using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Voice.Services.EchoDetection;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Tracks what the assistant is currently saying (TTS output).
/// Used to filter out assistant's own speech from transcriptions.
/// Orchestrates multiple echo detection strategies.
/// </summary>
public class AssistantSpeechTrackerService : IAssistantSpeechTrackerService
{
    private readonly ILogger<AssistantSpeechTrackerService> _logger;
    private readonly TtsHistoryTracker _historyTracker;
    private readonly TextNormalizer _textNormalizer;
    private readonly IEnumerable<IEchoDetectionStrategy> _strategies;

    public AssistantSpeechTrackerService(
        ILogger<AssistantSpeechTrackerService> logger,
        TtsHistoryTracker historyTracker,
        TextNormalizer textNormalizer,
        IEnumerable<IEchoDetectionStrategy> strategies)
    {
        _logger = logger;
        _historyTracker = historyTracker;
        _textNormalizer = textNormalizer;
        _strategies = strategies;
    }

    /// <summary>
    /// Returns true if assistant is currently speaking (TTS active).
    /// Used for AEC logging.
    /// </summary>
    public bool IsSpeaking => _historyTracker.IsSpeaking;

    /// <summary>
    /// Called when assistant starts speaking. Adds to history.
    /// </summary>
    public void StartSpeaking(string text)
    {
        _historyTracker.Add(text);
    }

    /// <summary>
    /// Called when assistant stops speaking.
    /// </summary>
    public void StopSpeaking()
    {
        _historyTracker.MarkStopped();
    }

    /// <summary>
    /// Filters out all TTS echo messages from the transcription.
    /// Iterates through TTS history and tries each detection strategy.
    /// </summary>
    public string FilterEchoFromTranscription(string transcription)
    {
        if (string.IsNullOrWhiteSpace(transcription))
            return transcription;

        var history = _historyTracker.GetHistory();
        if (history.Count == 0)
            return transcription;

        var result = transcription;
        var removedCount = 0;

        _logger.LogDebug("Filtering echo from: \"{Text}\", TTS History has {Count} message(s)",
            result.Length > 80 ? result[..80] + "..." : result, history.Count);

        // Iterate through TTS history and try to remove each from the beginning
        foreach (var ttsMessage in history)
        {
            var (wasRemoved, newResult, similarity) = TryRemoveEcho(result, ttsMessage);

            if (wasRemoved)
            {
                removedCount++;
                _logger.LogDebug("Removed echo (similarity: {Similarity:P0}): \"{Message}\"",
                    similarity, ttsMessage.Length > 50 ? ttsMessage[..50] + "..." : ttsMessage);
                result = newResult;

                // If nothing left, we're done
                if (string.IsNullOrWhiteSpace(result))
                    break;
            }
        }

        if (removedCount > 0)
        {
            _logger.LogDebug("Filtered {Count} echo(es). Result: \"{Result}\"",
                removedCount, result.Length > 80 ? result[..80] + "..." : result);
        }
        else
        {
            _logger.LogDebug("No echo detected! TTS history: {History}",
                string.Join(", ", history.Select(tts => $"\"{(tts.Length > 60 ? tts[..60] + "..." : tts)}\"")));
        }

        return result.Trim();
    }

    /// <summary>
    /// Tries to detect echo using all available strategies.
    /// Strategies are tried in order until one succeeds.
    /// </summary>
    private (bool wasRemoved, string newText, double similarity) TryRemoveEcho(string text, string ttsMessage)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(ttsMessage))
            return (false, text, 0.0);

        // Try each strategy in order
        foreach (var strategy in _strategies)
        {
            var result = strategy.DetectAndRemove(text, ttsMessage);
            if (result.wasRemoved)
            {
                _logger.LogDebug("[{StrategyName}] detected echo", strategy.StrategyName);
                return result;
            }
        }

        return (false, text, 0.0);
    }

    /// <summary>
    /// Gets the TTS history count (for debugging).
    /// </summary>
    public int GetHistoryCount()
    {
        return _historyTracker.GetHistoryCount();
    }

    /// <summary>
    /// Clears the TTS history.
    /// </summary>
    public void ClearHistory()
    {
        _historyTracker.Clear();
    }

    /// <summary>
    /// Checks if any TTS message in history contains one of the stop words.
    /// </summary>
    public bool ContainsStopWord(IEnumerable<string> stopWords)
    {
        return _historyTracker.ContainsStopWord(stopWords, _textNormalizer.Normalize);
    }

    // ========== LEGACY METHODS (kept for compatibility) ==========

    /// <summary>
    /// Checks if a transcription matches the assistant's recent speech.
    /// </summary>
    [Obsolete("Use FilterEchoFromTranscription() instead")]
    public bool IsAssistantSpeech(string transcription)
    {
        var filtered = FilterEchoFromTranscription(transcription);
        return string.IsNullOrWhiteSpace(filtered);
    }

    /// <summary>
    /// Gets the current/recent speech text (for debugging).
    /// </summary>
    [Obsolete("Use GetHistoryCount() instead")]
    public string? GetCurrentSpeechText()
    {
        var history = _historyTracker.GetHistory();
        return history.Count > 0 ? history[^1] : null;
    }

    /// <summary>
    /// Legacy method - now uses FilterEchoFromTranscription internally.
    /// </summary>
    [Obsolete("Use FilterEchoFromTranscription() instead")]
    public (bool isEcho, double similarity, string remainingText) DetectEchoAndExtractRemaining(string transcription)
    {
        var filtered = FilterEchoFromTranscription(transcription);
        var isFullEcho = string.IsNullOrWhiteSpace(filtered);
        return (isFullEcho, isFullEcho ? 1.0 : 0.0, filtered);
    }
}
