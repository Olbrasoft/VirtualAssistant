using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Voice.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// OpenCode Zen API provider for correcting Czech ASR transcriptions using alpha-glm-4.7 model.
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
        IQueryProcessor queryProcessor)
        : base(httpClient, promptCache, logger, desktopContextService, queryProcessor, options.Value.Enabled)
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

            var request = new
            {
                model = _options.Model,
                messages = new[]
                {
                    new { role = "system", content = promptText },
                    new { role = "user", content = text }
                },
                temperature = _options.Temperature,
                max_tokens = _options.MaxTokens
            };

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

            Logger.LogInformation("Zen correction completed in {Duration}ms using prompt ID {PromptId}, model ID {ModelId}. Original length: {OriginalLength}, Corrected length: {CorrectedLength}",
                durationMs, promptId, modelId, text.Length, correctedText.Length);

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
