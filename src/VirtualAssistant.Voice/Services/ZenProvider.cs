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
/// OpenCode Zen API provider for correcting Czech ASR transcriptions.
/// Supports models: kimi-k2, glm-4.7 (with reasoning_effort), claude-3-5-haiku, etc.
/// </summary>
public class ZenProvider : LlmProviderBase
{
    private readonly ZenOptions _options;

    public override string ProviderName => "zen";
    public override string ModelName => _options.Model;
    protected override bool ConfigEnabled => _options.Enabled;
    protected override int MinTextLength => _options.MinTextLengthForCorrection;
    protected override string ChatCompletionsEndpoint => "chat/completions";
    protected override ILlmProviderOptions Options => _options;

    public ZenProvider(
        HttpClient httpClient,
        IOptions<ZenOptions> options,
        IPromptCache promptCache,
        ILogger<ZenProvider> logger,
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

            // Dynamic max_tokens budget — see LlmProviderBase.CalculateMaxTokens
            // for the sizing model. Zen with reasoning_effort gets a small
            // reasoning buffer (500) to be safe. Non-reasoning case still
            // benefits from a generous floor.
            var reasoningBuffer = string.IsNullOrWhiteSpace(_options.ReasoningEffort) ? 0 : 500;
            const int providerCap = 16384;
            var maxTokens = CalculateMaxTokens(text, _options.MaxTokens, reasoningBuffer, providerCap);

            var request = new Dictionary<string, object>
            {
                ["model"] = _options.Model,
                ["messages"] = new[]
                {
                    new { role = "system", content = promptText },
                    new { role = "user", content = text }
                },
                ["temperature"] = _options.Temperature,
                ["max_tokens"] = maxTokens
            };

            if (!string.IsNullOrWhiteSpace(_options.ReasoningEffort))
            {
                request["reasoning_effort"] = _options.ReasoningEffort.Trim();
            }

            var response = await HttpClient.PostAsJsonAsync(ChatCompletionsEndpoint, request, cancellationToken);

            CaptureRateLimitHeaders(response);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ZenResponse>(cancellationToken);

            if (result?.Choices == null || result.Choices.Length == 0)
            {
                throw new InvalidOperationException("Zen API returned empty response");
            }

            var correctedText = result.Choices[0].Message.Content.Trim();
            var durationMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

            Logger.LogInformation("Zen correction completed in {Duration}ms using prompt ID {PromptId}, model ID {ModelId}. Original length: {OriginalLength}, Corrected length: {CorrectedLength}, max_tokens_sent: {MaxTokensSent}",
                durationMs, promptId, modelId, text.Length, correctedText.Length, maxTokens);

            // Detect likely truncation. We don't have completion_tokens from
            // Zen's response, so DetectTruncation will fall back to the
            // text-shape heuristics (mid-word ending, heavy compression).
            DetectTruncation(text, correctedText, completionTokens: null, maxTokens);

            return new LlmCorrectionResult(correctedText, promptId, durationMs, modelId);
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "HTTP error calling Zen API: {Message}", ex.Message);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            Logger.LogError(ex, "Zen API request timeout");
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error calling Zen API: {Message}", ex.Message);
            throw;
        }
    }

    private class ZenResponse
    {
        public ZenChoice[] Choices { get; set; } = Array.Empty<ZenChoice>();
    }

    private class ZenChoice
    {
        public ZenMessage Message { get; set; } = new();
    }

    private class ZenMessage
    {
        public string Content { get; set; } = string.Empty;
    }
}
