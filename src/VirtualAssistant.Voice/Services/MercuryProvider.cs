using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;
using Olbrasoft.VirtualAssistant.Voice.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Inception Labs Mercury 2 API provider for correcting Czech ASR transcriptions.
/// Mercury 2 is a diffusion-based LLM with OpenAI-compatible API.
/// Supports reasoning_effort parameter: instant, low, medium, high.
/// </summary>
public class MercuryProvider : LlmProviderBase
{
    private readonly MercuryOptions _options;

    public override string ProviderName => "mercury";
    public override string ModelName => _options.Model;
    protected override bool ConfigEnabled => _options.Enabled;
    protected override int MinTextLength => _options.MinTextLengthForCorrection;
    protected override string ChatCompletionsEndpoint => "chat/completions";
    protected override ILlmProviderOptions Options => _options;

    public MercuryProvider(
        HttpClient httpClient,
        IOptions<MercuryOptions> options,
        IPromptCache promptCache,
        ILogger<MercuryProvider> logger,
        IDesktopContextService desktopContextService,
        IQueryProcessor queryProcessor,
        ICliAppDetector cliAppDetector)
        : base(httpClient, promptCache, logger, desktopContextService, queryProcessor, cliAppDetector, options.Value.Enabled)
    {
        _options = options.Value;

        HttpClient.BaseAddress = new Uri(_options.BaseUrl);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        HttpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    public override async Task<LlmCorrectionResult> CorrectTextAsync(string text, CancellationToken cancellationToken = default)
    {
        var skipResult = CheckShouldSkip(text);
        if (skipResult != null)
            return skipResult;

        var startTime = DateTime.UtcNow;

        try
        {
            var (promptText, promptId) = await GetSystemPromptAsync(cancellationToken);
            var modelId = await GetModelIdAsync(cancellationToken);

            // Mercury 2 temperature must be in range 0.5-1.0
            var temperature = Math.Max(0.5, Math.Min(1.0, _options.Temperature));

            var request = new Dictionary<string, object>
            {
                ["model"] = _options.Model,
                ["messages"] = new[]
                {
                    new { role = "system", content = promptText },
                    new { role = "user", content = text }
                },
                ["temperature"] = temperature,
                ["max_tokens"] = _options.MaxTokens
            };

            if (!string.IsNullOrWhiteSpace(_options.ReasoningEffort))
            {
                request["reasoning_effort"] = _options.ReasoningEffort.Trim();
            }

            var response = await HttpClient.PostAsJsonAsync(ChatCompletionsEndpoint, request, cancellationToken);

            CaptureRateLimitHeaders(response);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<MercuryResponse>(cancellationToken);

            if (result?.Choices == null || result.Choices.Length == 0)
            {
                throw new InvalidOperationException("Mercury API returned empty choices");
            }

            var content = result.Choices[0].Message.Content;

            if (string.IsNullOrWhiteSpace(content))
            {
                // Mercury may consume all tokens on reasoning if max_tokens is too low
                var reasoningTokens = result.Usage?.ReasoningTokens ?? 0;
                Logger.LogWarning("Mercury returned empty content. Reasoning tokens: {ReasoningTokens}, Completion tokens: {CompletionTokens}. Consider increasing max_tokens or using reasoning_effort=instant",
                    reasoningTokens, result.Usage?.CompletionTokens ?? 0);
                throw new InvalidOperationException($"Mercury API returned empty content (reasoning used {reasoningTokens} tokens)");
            }

            var correctedText = content.Trim();
            var durationMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

            Logger.LogInformation("Mercury correction completed in {Duration}ms using prompt ID {PromptId}, model ID {ModelId}. " +
                "Original length: {OriginalLength}, Corrected length: {CorrectedLength}, " +
                "Tokens: input={InputTokens}, output={OutputTokens}, reasoning={ReasoningTokens}",
                durationMs, promptId, modelId, text.Length, correctedText.Length,
                result.Usage?.PromptTokens ?? 0, result.Usage?.CompletionTokens ?? 0, result.Usage?.ReasoningTokens ?? 0);

            return new LlmCorrectionResult(
                correctedText, promptId, durationMs, modelId,
                result.Usage?.PromptTokens, result.Usage?.CompletionTokens, result.Usage?.ReasoningTokens);
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "HTTP error calling Mercury API: {Message}", ex.Message);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            Logger.LogError(ex, "Mercury API request timeout");
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error calling Mercury API: {Message}", ex.Message);
            throw;
        }
    }

    private class MercuryResponse
    {
        [JsonPropertyName("choices")]
        public MercuryChoice[] Choices { get; set; } = Array.Empty<MercuryChoice>();

        [JsonPropertyName("usage")]
        public MercuryUsage? Usage { get; set; }
    }

    private class MercuryChoice
    {
        [JsonPropertyName("message")]
        public MercuryMessage Message { get; set; } = new();
    }

    private class MercuryMessage
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private class MercuryUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("reasoning_tokens")]
        public int ReasoningTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }
}
