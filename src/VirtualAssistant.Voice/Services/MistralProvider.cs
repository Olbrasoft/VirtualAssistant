using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;
using Olbrasoft.VirtualAssistant.Voice.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Mistral AI provider for correcting Czech ASR transcriptions.
/// </summary>
public class MistralProvider : LlmProviderBase
{
    private readonly MistralOptions _options;

    public override string ProviderName => "mistral";
    public override string ModelName => _options.Model;
    protected override bool ConfigEnabled => _options.Enabled;
    protected override int MinTextLength => _options.MinTextLengthForCorrection;
    protected override string ChatCompletionsEndpoint => "/v1/chat/completions";
    protected override ILlmProviderOptions Options => _options;

    public MistralProvider(
        HttpClient httpClient,
        IOptions<MistralOptions> options,
        IPromptCache promptCache,
        ILogger<MistralProvider> logger,
        IDesktopContextService desktopContextService,
        IQueryProcessor queryProcessor,
        ICliAppDetector cliAppDetector,
        IServiceScopeFactory scopeFactory)
        : base(httpClient, promptCache, logger, desktopContextService, queryProcessor, cliAppDetector, scopeFactory, options.Value.Enabled)
    {
        _options = options.Value;

        HttpClient.BaseAddress = new Uri(_options.BaseUrl);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        HttpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    public override async Task<LlmCorrectionResult> CorrectTextAsync(string text, CancellationToken cancellationToken = default)
    {
        var (promptText, promptId) = await GetSystemPromptAsync(cancellationToken);
        return await CorrectTextAsync(text, promptText, promptId, cancellationToken);
    }

    public override async Task<LlmCorrectionResult> CorrectTextAsync(string text, string promptText, int promptId, CancellationToken cancellationToken = default)
    {
        var skipResult = CheckShouldSkip(text);
        if (skipResult != null)
            return skipResult;

        var startTime = DateTime.UtcNow;

        try
        {
            var modelId = await GetModelIdAsync(cancellationToken);

            // Dynamic max_tokens budget — see LlmProviderBase.CalculateMaxTokens.
            // Mistral is not a reasoning model, so no reasoning buffer.
            const int reasoningBuffer = 0;
            const int providerCap = 16384;
            var maxTokens = CalculateMaxTokens(text, _options.MaxTokens, reasoningBuffer, providerCap);

            var request = new
            {
                model = _options.Model,
                messages = new[]
                {
                    new { role = "system", content = promptText },
                    new { role = "user", content = text }
                },
                temperature = _options.Temperature,
                max_tokens = maxTokens
            };

            var response = await HttpClient.PostAsJsonAsync(ChatCompletionsEndpoint, request, cancellationToken);

            CaptureRateLimitHeaders(response);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<MistralResponse>(cancellationToken);

            if (result?.Choices == null || result.Choices.Length == 0)
            {
                throw new InvalidOperationException("Mistral API returned empty response");
            }

            var correctedText = result.Choices[0].Message.Content.Trim();
            var durationMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

            Logger.LogInformation("Mistral correction completed in {Duration}ms using prompt ID {PromptId}, model ID {ModelId}. Original length: {OriginalLength}, Corrected length: {CorrectedLength}, max_tokens_sent: {MaxTokensSent}",
                durationMs, promptId, modelId, text.Length, correctedText.Length, maxTokens);

            // Detect likely truncation. Mistral's response doesn't expose
            // completion_tokens here, so DetectTruncation falls back to the
            // text-shape heuristics.
            DetectTruncation(text, correctedText, completionTokens: null, maxTokens);

            return new LlmCorrectionResult(correctedText, promptId, durationMs, modelId);
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "HTTP error calling Mistral API: {Message}", ex.Message);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            Logger.LogError(ex, "Mistral API request timeout");
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error calling Mistral API: {Message}", ex.Message);
            throw;
        }
    }

    private class MistralResponse
    {
        public MistralChoice[] Choices { get; set; } = Array.Empty<MistralChoice>();
    }

    private class MistralChoice
    {
        public MistralMessage Message { get; set; } = new();
    }

    private class MistralMessage
    {
        public string Content { get; set; } = string.Empty;
    }
}
