using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Voice.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Tracks TTS (Text-To-Speech) message history.
/// Used to filter out assistant's own speech from transcriptions.
/// </summary>
public class TtsHistoryTracker
{
    private readonly ILogger<TtsHistoryTracker> _logger;
    private readonly EchoDetectionOptions _options;
    private readonly object _lock = new();

    private readonly List<string> _ttsHistory = new();
    private DateTime _speakingStartedAt = DateTime.MinValue;

    public TtsHistoryTracker(
        ILogger<TtsHistoryTracker> logger,
        IOptions<EchoDetectionOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Returns true if assistant is currently speaking (TTS active).
    /// </summary>
    public bool IsSpeaking
    {
        get
        {
            lock (_lock)
            {
                if (_speakingStartedAt == DateTime.MinValue)
                    return false;

                // Consider stale after MaxSpeakingDurationSeconds
                var duration = DateTime.UtcNow - _speakingStartedAt;
                if (duration.TotalSeconds > _options.MaxSpeakingDurationSeconds)
                {
                    _speakingStartedAt = DateTime.MinValue;
                    return false;
                }

                return true;
            }
        }
    }

    /// <summary>
    /// Adds a TTS message to the history.
    /// </summary>
    public void Add(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        lock (_lock)
        {
            _ttsHistory.Add(text);
            _speakingStartedAt = DateTime.UtcNow;

            // Limit history size
            while (_ttsHistory.Count > _options.MaxHistorySize)
            {
                _ttsHistory.RemoveAt(0);
            }

            _logger.LogDebug("TTS History [{Count}]: \"{Text}\"",
                _ttsHistory.Count,
                text.Length > 60 ? text[..60] + "..." : text);
        }
    }

    /// <summary>
    /// Marks that TTS has stopped speaking.
    /// </summary>
    public void MarkStopped()
    {
        lock (_lock)
        {
            _speakingStartedAt = DateTime.MinValue;
        }
    }

    /// <summary>
    /// Gets a snapshot of the current TTS history.
    /// </summary>
    public IReadOnlyList<string> GetHistory()
    {
        lock (_lock)
        {
            return _ttsHistory.ToList();
        }
    }

    /// <summary>
    /// Gets the number of messages in history.
    /// </summary>
    public int GetHistoryCount()
    {
        lock (_lock)
        {
            return _ttsHistory.Count;
        }
    }

    /// <summary>
    /// Clears the TTS history.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _ttsHistory.Clear();
        }
    }

    /// <summary>
    /// Checks if any TTS message in history contains one of the stop words.
    /// </summary>
    public bool ContainsStopWord(IEnumerable<string> stopWords, Func<string, string> normalizeFunc)
    {
        lock (_lock)
        {
            if (_ttsHistory.Count == 0)
                return false;

            foreach (var ttsMessage in _ttsHistory)
            {
                var normalized = normalizeFunc(ttsMessage);
                foreach (var stopWord in stopWords)
                {
                    var normalizedStopWord = stopWord.ToLowerInvariant();
                    if (normalized.Contains(normalizedStopWord))
                    {
                        _logger.LogDebug("Found stop word '{StopWord}' in TTS history: {Message}",
                            stopWord, ttsMessage);
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
