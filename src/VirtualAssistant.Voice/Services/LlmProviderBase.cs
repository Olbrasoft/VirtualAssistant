using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;
using Olbrasoft.VirtualAssistant.Data.Queries.LlmModelQueries;
using Olbrasoft.VirtualAssistant.Data.Queries.PromptQueries;
using Olbrasoft.VirtualAssistant.Voice.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Abstract base class for LLM providers with shared functionality.
/// </summary>
public abstract class LlmProviderBase : ILlmProvider
{
    protected readonly HttpClient HttpClient;
    protected readonly ILogger Logger;
    protected readonly IPromptCache PromptCache;
    protected readonly IDesktopContextService DesktopContextService;
    protected readonly IQueryProcessor QueryProcessor;
    protected readonly ICliAppDetector CliAppDetector;
    private readonly IServiceScopeFactory _scopeFactory;

    private Dictionary<string, string> _lastRateLimitHeaders = new();
    private bool _runtimeEnabled;
    private int? _cachedModelId;

    /// <summary>
    /// Gets the provider name identifier (e.g., "zen", "mistral").
    /// </summary>
    public abstract string ProviderName { get; }

    /// <summary>
    /// Gets the model name being used.
    /// </summary>
    public abstract string ModelName { get; }

    /// <summary>
    /// Gets whether the provider is enabled in configuration.
    /// </summary>
    protected abstract bool ConfigEnabled { get; }

    /// <summary>
    /// Gets the minimum text length for correction.
    /// </summary>
    protected abstract int MinTextLength { get; }

    /// <summary>
    /// Gets the API endpoint path for chat completions.
    /// </summary>
    protected abstract string ChatCompletionsEndpoint { get; }

    /// <summary>
    /// Gets the provider-specific options.
    /// </summary>
    protected abstract ILlmProviderOptions Options { get; }

    protected LlmProviderBase(
        HttpClient httpClient,
        IPromptCache promptCache,
        ILogger logger,
        IDesktopContextService desktopContextService,
        IQueryProcessor queryProcessor,
        ICliAppDetector cliAppDetector,
        IServiceScopeFactory scopeFactory,
        bool initialEnabled)
    {
        HttpClient = httpClient;
        PromptCache = promptCache ?? throw new ArgumentNullException(nameof(promptCache));
        Logger = logger;
        DesktopContextService = desktopContextService ?? throw new ArgumentNullException(nameof(desktopContextService));
        QueryProcessor = queryProcessor ?? throw new ArgumentNullException(nameof(queryProcessor));
        CliAppDetector = cliAppDetector ?? throw new ArgumentNullException(nameof(cliAppDetector));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _runtimeEnabled = initialEnabled;
    }

    /// <summary>
    /// Gets the system prompt based on current desktop context.
    /// Priority: 1) CLI app detection (Claude Code, OpenCode), 2) Window title/app pattern, 3) Default prompt.
    /// Returns (promptText, promptId) tuple.
    /// </summary>
    protected async Task<(string PromptText, int PromptId)> GetSystemPromptAsync(CancellationToken ct)
    {
        try
        {
            var context = await DesktopContextService.GetCurrentContextAsync(ct);

            // Priority 1: Check for CLI apps running in terminals (e.g., Claude Code, OpenCode)
            // This handles cases where CLI apps run in terminal but don't change window title
            var cliApp = await CliAppDetector.DetectCliAppAsync(ct);
            if (cliApp != null)
            {
                Logger.LogDebug("CLI app detected: {AppName} → using prompt '{Prompt}'",
                    cliApp.AppName, cliApp.PromptFileName);

                // Get prompt ID from database by file name
                var cliPrompt = await QueryProcessor.ProcessAsync(
                    new GetPromptByFileNameQuery(cliApp.PromptFileName), ct);

                if (cliPrompt != null)
                {
                    var cliPromptText = PromptCache.GetPrompt(cliPrompt.PromptFileName);
                    Logger.LogDebug("Using prompt '{Prompt}' (ID: {Id}) for CLI app '{App}'",
                        cliPrompt.PromptFileName, cliPrompt.Id, cliApp.AppName);
                    return (cliPromptText, cliPrompt.Id);
                }

                // CLI app detected but no matching prompt in DB - use prompt file directly with ID 0
                Logger.LogWarning("CLI app '{App}' detected but prompt '{Prompt}' not found in database",
                    cliApp.AppName, cliApp.PromptFileName);
            }

            // Priority 2: Match by window title or application pattern
            Logger.LogDebug("Active window: '{Title}', app: '{App}', looking for matching prompt pattern",
                context.ActiveWindowTitle, context.ActiveApplication);

            var prompt = await QueryProcessor.ProcessAsync(
                new GetPromptByAppIdPatternQuery(context.ActiveWindowTitle, context.ActiveApplication), ct);

            // Priority 3: Default prompt
            prompt ??= await QueryProcessor.ProcessAsync(new GetDefaultPromptQuery(), ct);

            if (prompt == null)
            {
                Logger.LogError("CRITICAL: No default prompt found in database - using hardcoded fallback");
                return (PromptCache.GetPrompt("DefaultCorrection"), 4);
            }

            var promptText = PromptCache.GetPrompt(prompt.PromptFileName);

            Logger.LogDebug("Using prompt '{Prompt}' (ID: {Id}) for window '{Title}'",
                prompt.PromptFileName, prompt.Id, context.ActiveWindowTitle);

            return (promptText, prompt.Id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting context-aware prompt, falling back to DefaultCorrection (ID 4)");
            return (PromptCache.GetPrompt("DefaultCorrection"), 4);
        }
    }

    /// <summary>
    /// Gets the ModelId from database based on configured ModelIdentifier.
    /// Caches the result for subsequent calls.
    /// </summary>
    protected async Task<int?> GetModelIdAsync(CancellationToken ct)
    {
        if (_cachedModelId.HasValue)
            return _cachedModelId;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var queryProcessor = scope.ServiceProvider.GetRequiredService<IQueryProcessor>();

            var model = await queryProcessor.ProcessAsync(
                new GetLlmModelByIdentifierQuery(ModelName), ct);

            if (model != null)
            {
                _cachedModelId = model.Id;
                Logger.LogDebug("Resolved ModelId {ModelId} for model identifier '{ModelIdentifier}'",
                    model.Id, ModelName);
            }
            else
            {
                Logger.LogWarning("LlmModel with identifier '{ModelIdentifier}' not found in database. " +
                    "ModelId will be NULL in correction results.", ModelName);
            }

            return _cachedModelId;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error resolving ModelId for '{ModelIdentifier}'", ModelName);
            return null;
        }
    }

    /// <summary>
    /// Sets the runtime enabled state for LLM correction.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        _runtimeEnabled = enabled;
        Logger.LogInformation("{ProviderName} LLM correction {Status}", ProviderName, enabled ? "enabled" : "disabled");
    }

    /// <summary>
    /// Gets the current runtime enabled state.
    /// </summary>
    public bool IsEnabled() => _runtimeEnabled;

    /// <summary>
    /// Reloads all correction prompts by clearing the cache.
    /// </summary>
    public void ReloadPrompt()
    {
        PromptCache.ClearCache();
        Logger.LogInformation("{ProviderName} prompt cache cleared, prompts will reload on next request", ProviderName);
    }

    /// <summary>
    /// Gets the API usage information from the last response headers.
    /// </summary>
    public Dictionary<string, string> GetLastRateLimitHeaders()
    {
        return new Dictionary<string, string>(_lastRateLimitHeaders);
    }

    /// <summary>
    /// Captures rate limit headers from response.
    /// </summary>
    protected void CaptureRateLimitHeaders(HttpResponseMessage response)
    {
        _lastRateLimitHeaders = new Dictionary<string, string>();
        foreach (var header in response.Headers.Where(h => h.Key.StartsWith("x-ratelimit-", StringComparison.OrdinalIgnoreCase)))
        {
            _lastRateLimitHeaders[header.Key] = string.Join(", ", header.Value);
        }
    }

    /// <summary>
    /// Corrects the given text using the LLM provider. Resolves prompt internally.
    /// </summary>
    public abstract Task<LlmCorrectionResult> CorrectTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Corrects the given text using a pre-resolved prompt.
    /// Use this overload in racing mode to avoid concurrent DbContext access.
    /// </summary>
    public abstract Task<LlmCorrectionResult> CorrectTextAsync(string text, string promptText, int promptId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if correction should be skipped and returns the skip result if so.
    /// Returns null if correction should proceed.
    /// </summary>
    protected LlmCorrectionResult? CheckShouldSkip(string text)
    {
        if (!_runtimeEnabled)
        {
            Logger.LogDebug("Skipping LLM correction - {ProviderName} is disabled (runtime)", ProviderName);
            return new LlmCorrectionResult(text, null, 0);
        }

        if (!ConfigEnabled)
        {
            Logger.LogDebug("Skipping LLM correction - {ProviderName} is disabled (config)", ProviderName);
            return new LlmCorrectionResult(text, null, 0);
        }

        if (text.Length < MinTextLength)
        {
            Logger.LogDebug("Skipping LLM correction - text length {Length} < {MinLength}",
                text.Length, MinTextLength);
            return new LlmCorrectionResult(text, null, 0);
        }

        return null;
    }

    /// <summary>
    /// Calculates a dynamic <c>max_tokens</c> budget for the upcoming LLM
    /// request based on the user input length and provider characteristics.
    ///
    /// The configured <c>options.MaxTokens</c> is treated as a lower bound /
    /// minimum budget — for short dictations the configured value is used.
    /// For long dictations the budget grows so the LLM has enough headroom
    /// to emit the full corrected text without mid-word truncation.
    ///
    /// Sizing model:
    ///   - Czech text averages ~3 characters per token
    ///   - Output length ≈ input length (slight stylistic compression)
    ///   - Reasoning models (Mercury) need a separate buffer on top of
    ///     the visible output, so callers should pass <c>reasoningBuffer</c>
    ///     for that
    ///   - Final cap is hard-clamped to <c>maxAllowed</c> (provider-specific
    ///     API ceiling, e.g. 16384) so we never exceed what the API accepts
    ///
    /// Real-world calibration: incident on voice_transcriptions.id=11577 had
    /// 1756-char Whisper input + Mercury 2 max_tokens=1000 + reasoning_effort=low
    /// → output truncated mid-word at 845 chars / 276 tokens. With this method
    /// the same input gets ~1750 output budget + 1500 reasoning buffer = 3250
    /// max_tokens, which fits comfortably below the 4096 cap.
    /// </summary>
    /// <param name="text">The user input text to be corrected.</param>
    /// <param name="configuredMin">The configured base MaxTokens value
    /// (acts as a floor for short inputs).</param>
    /// <param name="reasoningBuffer">Extra tokens to reserve for internal
    /// reasoning (Mercury 2: pass ~1500; non-reasoning models: 0).</param>
    /// <param name="maxAllowed">Hard ceiling enforced by the provider's API.</param>
    protected int CalculateMaxTokens(string text, int configuredMin, int reasoningBuffer, int maxAllowed)
    {
        // Estimate the output token count from the input length. Czech with
        // diacritics in UTF-8 averages around 2.5 chars per token (lower
        // than the 3-4 chars/token typical for English) because each
        // diacritical letter takes its own subword token. We use 2.5 as
        // the divisor — slightly conservative — to give the output room
        // without consuming budget for headroom that the floor (configuredMin)
        // already provides.
        var estimatedOutputTokens = (int)Math.Ceiling(text.Length / 2.5);

        var dynamic = estimatedOutputTokens + reasoningBuffer;
        var withFloor = Math.Max(dynamic, configuredMin);
        var clamped = Math.Min(withFloor, maxAllowed);

        if (withFloor > maxAllowed)
        {
            Logger.LogWarning(
                "{ProviderName}: input length {InputChars} chars would need ~{Estimated} tokens " +
                "(reasoning buffer {ReasoningBuffer}), but provider cap is {MaxAllowed}. " +
                "Output may be truncated.",
                ProviderName, text.Length, withFloor, reasoningBuffer, maxAllowed);
        }

        return clamped;
    }

    /// <summary>
    /// Detects whether an LLM response was likely truncated due to a
    /// max_tokens cap. Logs a warning if so. Truncation indicators:
    ///   1. <paramref name="completionTokens"/> is at or above 95% of the
    ///      max_tokens budget that was sent in the request
    ///   2. The corrected text is significantly shorter than the input
    ///      (less than 50%) AND ends without terminal punctuation, suggesting
    ///      the model stopped mid-thought
    ///   3. The corrected text ends mid-word (no whitespace before the last
    ///      character and no terminal punctuation)
    /// </summary>
    /// <returns>true if the response looks truncated.</returns>
    protected bool DetectTruncation(string inputText, string correctedText, int? completionTokens, int maxTokensSent)
    {
        if (string.IsNullOrEmpty(correctedText)) return false;

        var truncated = false;
        var reasons = new List<string>();

        // Reason 1: completion_tokens hit the cap
        if (completionTokens.HasValue && maxTokensSent > 0)
        {
            var ratio = (double)completionTokens.Value / maxTokensSent;
            if (ratio >= 0.95)
            {
                truncated = true;
                reasons.Add($"completion_tokens {completionTokens.Value} is {ratio:P0} of max_tokens {maxTokensSent}");
            }
        }

        // Reason 2: heavy compression + missing terminal punctuation
        var lastChar = correctedText[correctedText.Length - 1];
        var endsWithTerminal = lastChar == '.' || lastChar == '!' || lastChar == '?' ||
                               lastChar == '"' || lastChar == ')' || lastChar == ']' ||
                               lastChar == '…';
        if (inputText.Length > 200 && correctedText.Length < inputText.Length / 2 && !endsWithTerminal)
        {
            truncated = true;
            reasons.Add($"corrected length {correctedText.Length} < 50% of input {inputText.Length} and no terminal punctuation");
        }

        // Reason 3: ends mid-word
        if (!endsWithTerminal && !char.IsWhiteSpace(lastChar) && correctedText.Length > 0)
        {
            // Find the last whitespace character (any kind — \n, \t, NBSP,
            // not just literal ' '); if it is far from the end, the final
            // word is unusually long, suggesting we cut a real word in half.
            var lastWhitespaceIndex = -1;
            for (var i = correctedText.Length - 1; i >= 0; i--)
            {
                if (char.IsWhiteSpace(correctedText[i]))
                {
                    lastWhitespaceIndex = i;
                    break;
                }
            }
            var trailingWordLength = lastWhitespaceIndex < 0
                ? correctedText.Length
                : correctedText.Length - lastWhitespaceIndex - 1;
            if (trailingWordLength > 25)
            {
                truncated = true;
                reasons.Add($"trailing word is {trailingWordLength} chars (likely cut mid-word)");
            }
        }

        if (truncated)
        {
            Logger.LogWarning(
                "{ProviderName}: LIKELY TRUNCATED correction. Input {InputLength} chars → " +
                "corrected {CorrectedLength} chars. Reasons: {Reasons}",
                ProviderName, inputText.Length, correctedText.Length, string.Join("; ", reasons));
        }

        return truncated;
    }
}
