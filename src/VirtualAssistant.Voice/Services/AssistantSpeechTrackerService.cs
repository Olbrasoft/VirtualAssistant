using System.Globalization;
using System.Text;
using Olbrasoft.VirtualAssistant.Voice.Similarity;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Tracks what the assistant is currently saying (TTS output).
/// Used to filter out assistant's own speech from transcriptions.
/// Now supports multiple TTS messages that arrive in sequence.
/// </summary>
public class AssistantSpeechTrackerService : IAssistantSpeechTrackerService
{
    private readonly ILogger<AssistantSpeechTrackerService> _logger;
    private readonly IStringSimilarity _stringSimilarity;
    private readonly object _lock = new();
    
    // History of TTS messages (multiple can arrive before Whisper returns)
    private readonly List<string> _ttsHistory = new();
    private const int MaxHistorySize = 10;
    
    // Similarity threshold for fuzzy matching (0.0 - 1.0)
    // Lowered from 0.75 to 0.70 to catch more Whisper transcription variants
    private const double SimilarityThreshold = 0.70;
    
    // Track if TTS is currently playing (for AEC logging)
    private DateTime _speakingStartedAt = DateTime.MinValue;
    private const int MaxSpeakingDurationSeconds = 60;
    
    /// <summary>
    /// Returns true if assistant is currently speaking (TTS active).
    /// Used for AEC logging.
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
                if (duration.TotalSeconds > MaxSpeakingDurationSeconds)
                {
                    _speakingStartedAt = DateTime.MinValue;
                    return false;
                }
                
                return true;
            }
        }
    }

    public AssistantSpeechTrackerService(ILogger<AssistantSpeechTrackerService> logger, IStringSimilarity stringSimilarity)
    {
        _logger = logger;
        _stringSimilarity = stringSimilarity;
    }

    /// <summary>
    /// Called when assistant starts speaking. Adds to history instead of replacing.
    /// </summary>
    public void StartSpeaking(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
            
        lock (_lock)
        {
            _ttsHistory.Add(text);
            _speakingStartedAt = DateTime.UtcNow;
            
            // Limit history size
            while (_ttsHistory.Count > MaxHistorySize)
            {
                _ttsHistory.RemoveAt(0);
            }
            
            _logger.LogDebug("TTS History [{Count}]: \"{Text}\"", _ttsHistory.Count, text.Length > 60 ? text[..60] + "..." : text);
        }
    }

    /// <summary>
    /// Called when assistant stops speaking.
    /// </summary>
    public void StopSpeaking()
    {
        lock (_lock)
        {
            _speakingStartedAt = DateTime.MinValue;
        }
    }

    /// <summary>
    /// Filters out all TTS echo messages from the transcription.
    /// Iterates through TTS history and removes matching prefixes.
    /// </summary>
    /// <param name="transcription">The full transcription from Whisper</param>
    /// <returns>Cleaned text with TTS echoes removed</returns>
    public string FilterEchoFromTranscription(string transcription)
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(transcription))
                return transcription;
                
            if (_ttsHistory.Count == 0)
                return transcription;
            
            var result = transcription;
            var removedCount = 0;
            
            _logger.LogDebug("Filtering echo from: \"{Text}\", TTS History has {Count} message(s)",
                result.Length > 80 ? result[..80] + "..." : result, _ttsHistory.Count);
            
            // Iterate through TTS history and try to remove each from the beginning
            foreach (var ttsMessage in _ttsHistory)
            {
                var (wasRemoved, newResult, similarity) = TryRemovePrefix(result, ttsMessage);
                
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
            
            // DON'T clear history here - it will be cleared before sending to LLM hub
            
            if (removedCount > 0)
            {
                _logger.LogDebug("Filtered {Count} echo(es). Result: \"{Result}\"",
                    removedCount, result.Length > 80 ? result[..80] + "..." : result);
            }
            else
            {
                _logger.LogDebug("No echo detected! TTS history: {History}",
                    string.Join(", ", _ttsHistory.Select(tts => $"\"{(tts.Length > 60 ? tts[..60] + "..." : tts)}\"")));
            }
            
            return result.Trim();
        }
    }

    /// <summary>
    /// Tries to detect if the text is an echo of a TTS message using multiple strategies:
    /// 1. Prefix matching (text starts with TTS content)
    /// 2. Substring matching (text is contained within TTS content - for partial captures)
    /// </summary>
    private (bool wasRemoved, string newText, double similarity) TryRemovePrefix(string text, string ttsMessage)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(ttsMessage))
            return (false, text, 0.0);
            
        var textNormalized = NormalizeText(text);
        var ttsNormalized = NormalizeText(ttsMessage);
        
        var textWords = textNormalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var ttsWords = ttsNormalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (textWords.Length == 0 || ttsWords.Length == 0)
            return (false, text, 0.0);
        
        // Strategy 1: Check if text is a SUBSTRING of TTS (Whisper captured only part of TTS)
        // This handles cases where Whisper starts recording mid-sentence
        // ONLY use this when the ENTIRE text is contained in TTS (no user speech appended)
        var textNormalizedForContains = RemoveDiacritics(textNormalized.ToLowerInvariant());
        var ttsNormalizedForContains = RemoveDiacritics(ttsNormalized.ToLowerInvariant());
        
        if (ttsNormalizedForContains.Contains(textNormalizedForContains) && textWords.Length >= 3)
        {
            _logger.LogDebug("Substring match! Text is contained in TTS message");
            // The entire captured text is part of TTS output - it's an echo
            return (true, string.Empty, 1.0);
        }
        
        // Strategy 2: Check if text STARTS WITH TTS content (fuzzy prefix matching for partial captures)
        // Count consecutive matching words from the beginning
        var consecutiveMatches = 0;
        for (int i = 0; i < textWords.Length && i < ttsWords.Length; i++)
        {
            if (CalculateSimilarity(textWords[i], ttsWords[i]) > 0.8)
            {
                consecutiveMatches++;
            }
            else
            {
                break; // Stop at first non-match
            }
        }
        
        // If we matched significant portion of TTS from the beginning, remove those words
        var ttsMatchRatio = ttsWords.Length > 0 ? (double)consecutiveMatches / ttsWords.Length : 0;
        if (consecutiveMatches >= 3 && ttsMatchRatio >= 0.6)
        {
            _logger.LogDebug("Fuzzy prefix match! {ConsecutiveMatches} consecutive words match TTS ({TtsMatchRatio:P0} of TTS)",
                consecutiveMatches, ttsMatchRatio);
            var remainingText = RemoveWordsFromOriginal(text, consecutiveMatches);
            return (true, remainingText, ttsMatchRatio);
        }
            
        // Strategy 3: Original prefix matching (text starts with TTS content)
        // Take prefix from text with same word count as TTS (+/- some tolerance)
        // We allow some tolerance because Whisper might add/remove words
        var minPrefixLen = Math.Max(1, ttsWords.Length - 2);
        var maxPrefixLen = Math.Min(textWords.Length, ttsWords.Length + 2);
        
        double bestSimilarity = 0;
        int bestPrefixLength = 0;
        
        // Try different prefix lengths and find best match
        for (int prefixLen = minPrefixLen; prefixLen <= maxPrefixLen; prefixLen++)
        {
            var prefix = string.Join(" ", textWords.Take(prefixLen));
            var similarity = CalculateSimilarity(prefix, ttsNormalized);

            _logger.LogDebug("prefixLen={PrefixLen}, similarity={Similarity:P1}, prefix=\"{Prefix}\"",
                prefixLen, similarity, prefix);

            if (similarity > bestSimilarity)
            {
                bestSimilarity = similarity;
                bestPrefixLength = prefixLen;
            }
        }

        _logger.LogDebug("Best: prefixLen={BestPrefixLength}, similarity={BestSimilarity:P1}, threshold={Threshold:P0}, TTS normalized: \"{TtsNormalized}\"",
            bestPrefixLength, bestSimilarity, SimilarityThreshold, ttsNormalized);
        
        if (bestSimilarity >= SimilarityThreshold)
        {
            // Remove the prefix from original text
            // We need to find where to cut in the ORIGINAL text (not normalized)
            var remainingText = RemoveWordsFromOriginal(text, bestPrefixLength);
            return (true, remainingText, bestSimilarity);
        }
        
        return (false, text, bestSimilarity);
    }

    /// <summary>
    /// Removes N words from the beginning of the original text (preserving original formatting).
    /// </summary>
    private static string RemoveWordsFromOriginal(string text, int wordCount)
    {
        if (string.IsNullOrWhiteSpace(text) || wordCount <= 0)
            return text;
            
        // Split by whitespace but keep track of positions
        var words = new List<(int start, int end)>();
        int i = 0;
        
        while (i < text.Length)
        {
            // Skip whitespace
            while (i < text.Length && char.IsWhiteSpace(text[i]))
                i++;
                
            if (i >= text.Length)
                break;
                
            int wordStart = i;
            
            // Find end of word
            while (i < text.Length && !char.IsWhiteSpace(text[i]))
                i++;
                
            words.Add((wordStart, i));
        }
        
        if (wordCount >= words.Count)
            return string.Empty;
            
        // Return text starting after the Nth word
        int cutPosition = words[wordCount].start;
        return text[cutPosition..].TrimStart();
    }

    /// <summary>
    /// Normalizes text for comparison (lowercase, remove punctuation, trim).
    /// </summary>
    private static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Convert to lowercase and remove common punctuation
        var normalized = text.ToLowerInvariant()
            .Replace(".", "")
            .Replace(",", "")
            .Replace("!", "")
            .Replace("?", "")
            .Replace(":", "")
            .Replace(";", "")
            .Replace("\"", "")
            .Replace("'", "")
            .Replace("\u201E", "") // Czech opening quote „
            .Replace("\u201C", "") // Czech closing quote "
            .Replace("(", "")
            .Replace(")", "")
            .Replace("[", "")
            .Replace("]", "")
            .Trim();

        // Normalize whitespace
        while (normalized.Contains("  "))
        {
            normalized = normalized.Replace("  ", " ");
        }

        return normalized;
    }

    /// <summary>
    /// Calculates similarity between two strings using the injected similarity algorithm.
    /// Returns value between 0.0 (no match) and 1.0 (perfect match).
    /// </summary>
    private double CalculateSimilarity(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return 0.0;

        // Normalize both strings (remove diacritics for Czech language support)
        var normalizedA = RemoveDiacritics(a.ToLowerInvariant());
        var normalizedB = RemoveDiacritics(b.ToLowerInvariant());

        return _stringSimilarity.Similarity(normalizedA, normalizedB);
    }

    /// <summary>
    /// Removes diacritics from text (e.g., "úspěšně" → "uspesne").
    /// Essential for comparing Czech text with potential ASR transcription errors.
    /// </summary>
    private static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        
        foreach (char c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Gets the TTS history count (for debugging).
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
    public void ClearHistory()
    {
        lock (_lock)
        {
            _ttsHistory.Clear();
        }
    }

    /// <summary>
    /// Checks if any TTS message in history contains one of the stop words.
    /// Used to distinguish between user's "stop" command and TTS echo.
    /// </summary>
    /// <param name="stopWords">Collection of stop words to check for</param>
    /// <returns>True if any TTS message contains a stop word</returns>
    public bool ContainsStopWord(IEnumerable<string> stopWords)
    {
        lock (_lock)
        {
            if (_ttsHistory.Count == 0)
                return false;
                
            foreach (var ttsMessage in _ttsHistory)
            {
                var normalized = NormalizeText(ttsMessage);
                foreach (var stopWord in stopWords)
                {
                    var normalizedStopWord = stopWord.ToLowerInvariant();
                    // Check if the stop word is present as a whole word
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
        lock (_lock)
        {
            return _ttsHistory.Count > 0 ? _ttsHistory[^1] : null;
        }
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
