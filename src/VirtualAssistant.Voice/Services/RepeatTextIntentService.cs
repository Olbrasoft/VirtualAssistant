using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Voice.Similarity;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Service for detecting "repeat last text" intent using fuzzy string matching.
/// No LLM calls - fast, deterministic, works offline.
/// </summary>
public interface IRepeatTextIntentService
{
    /// <summary>
    /// Checks if the user wants to repeat the last dictated text.
    /// </summary>
    /// <param name="inputText">The transcribed text to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if user wants to repeat text, false otherwise</returns>
    Task<RepeatTextIntentResult> DetectIntentAsync(string inputText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a random response message for clipboard confirmation.
    /// </summary>
    string GetRandomClipboardResponse();
}

/// <summary>
/// Result of repeat text intent detection.
/// </summary>
public record RepeatTextIntentResult
{
    public bool IsRepeatTextIntent { get; init; }
    public float Confidence { get; init; }
    public string? Reason { get; init; }
    public int ResponseTimeMs { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static RepeatTextIntentResult NotRepeat(string? reason = null, int responseTimeMs = 0)
        => new() { IsRepeatTextIntent = false, Reason = reason, ResponseTimeMs = responseTimeMs, Success = true };

    public static RepeatTextIntentResult Repeat(float confidence, string? reason, int responseTimeMs)
        => new() { IsRepeatTextIntent = true, Confidence = confidence, Reason = reason, ResponseTimeMs = responseTimeMs, Success = true };

    public static RepeatTextIntentResult Error(string errorMessage, int responseTimeMs = 0)
        => new() { Success = false, ErrorMessage = errorMessage, ResponseTimeMs = responseTimeMs };
}

/// <summary>
/// Implementation using fuzzy string matching for intent detection.
/// Matches against known phrases like "vrať mi to do schránky".
/// </summary>
public class RepeatTextIntentService : IRepeatTextIntentService
{
    private readonly ILogger<RepeatTextIntentService> _logger;
    private readonly IStringSimilarity _similarity;
    private readonly IPromptLoader _promptLoader;
    private readonly string[] _clipboardResponses;

    /// <summary>
    /// Phrases that indicate clipboard/repeat intent.
    /// Includes common Whisper transcription variants.
    /// </summary>
    private static readonly string[] TargetPhrases =
    [
        // Standard phrases
        "vrať mi to do schránky",
        "vrať to do schránky",
        "dej to do schránky",
        "zkopíruj to do schránky",
        "vrať mi to",
        "dej mi to zpátky",
        "znovu do schránky",
        "opakuj do schránky",
        // Short variants (better for fuzzy matching)
        "do schránky",
        "schránka",
        // Common Whisper misrecognitions
        "bred mi to do schránky",
        "vrech mi to do schránky",
        "rektor schránky",
        "vraťte do schránky",
        "vrátněte do schránky"
    ];

    /// <summary>
    /// Minimum similarity threshold (0.65 = 65%).
    /// Lowered to accommodate Whisper transcription errors.
    /// </summary>
    private const double SimilarityThreshold = 0.65;

    public RepeatTextIntentService(
        ILogger<RepeatTextIntentService> logger,
        IStringSimilarity similarity,
        IPromptLoader promptLoader)
    {
        _logger = logger;
        _similarity = similarity;
        _promptLoader = promptLoader;

        // Load clipboard responses from prompt file
        try
        {
            var responsesContent = _promptLoader.LoadPrompt("ClipboardResponses");
            _clipboardResponses = responsesContent
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();

            _logger.LogInformation(
                "RepeatTextIntentService initialized with fuzzy matching (threshold: {Threshold}%), {ResponseCount} responses loaded",
                SimilarityThreshold * 100,
                _clipboardResponses.Length);
        }
        catch (FileNotFoundException)
        {
            _logger.LogWarning("ClipboardResponses.md not found, using default responses");
            _clipboardResponses = ["Hotovo.", "Ok, je to tam.", "Zkopírováno."];
        }
    }

    public Task<RepeatTextIntentResult> DetectIntentAsync(string inputText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(inputText))
        {
            return Task.FromResult(RepeatTextIntentResult.NotRepeat("Empty input"));
        }

        var stopwatch = Stopwatch.StartNew();

        var normalizedInput = inputText.Trim().ToLowerInvariant();

        // Find best matching phrase
        var bestMatch = TargetPhrases
            .Select(phrase => new
            {
                Phrase = phrase,
                Similarity = _similarity.Similarity(normalizedInput, phrase)
            })
            .MaxBy(x => x.Similarity);

        stopwatch.Stop();
        var elapsedMs = (int)stopwatch.ElapsedMilliseconds;

        if (bestMatch != null && bestMatch.Similarity >= SimilarityThreshold)
        {
            _logger.LogInformation(
                "Repeat text intent DETECTED: '{Input}' matches '{Phrase}' with {Similarity:P0} similarity ({ElapsedMs}ms)",
                inputText, bestMatch.Phrase, bestMatch.Similarity, elapsedMs);

            return Task.FromResult(RepeatTextIntentResult.Repeat(
                (float)bestMatch.Similarity,
                $"Matches '{bestMatch.Phrase}'",
                elapsedMs));
        }

        _logger.LogDebug(
            "No repeat intent: '{Input}' best match '{Phrase}' with {Similarity:P0} (threshold: {Threshold:P0}, {ElapsedMs}ms)",
            inputText,
            bestMatch?.Phrase ?? "none",
            bestMatch?.Similarity ?? 0,
            SimilarityThreshold,
            elapsedMs);

        return Task.FromResult(RepeatTextIntentResult.NotRepeat(
            bestMatch != null ? $"Best match '{bestMatch.Phrase}' at {bestMatch.Similarity:P0}" : "No match",
            elapsedMs));
    }

    public string GetRandomClipboardResponse()
    {
        if (_clipboardResponses.Length == 0)
        {
            return "Hotovo.";
        }

        return _clipboardResponses[Random.Shared.Next(_clipboardResponses.Length)];
    }
}
