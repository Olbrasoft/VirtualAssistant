using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Voice.Filters;

/// <summary>
/// Filter strategy that removes Whisper hallucinations using two safer matching modes
/// than the simple substring match used by <see cref="RemovePatternsFilterStrategy"/>:
/// <list type="number">
///   <item>
///     <description>
///       <c>removeWholeText</c>: returns an empty string if the entire transcription
///       (after <see cref="string.Trim()"/>) matches one of the configured patterns
///       case-insensitively. Used for short hallucinations like "Konec." or "Děkuji."
///       which must not be filtered when they appear inside longer legitimate text
///       (e.g. "Konec konců...").
///     </description>
///   </item>
///   <item>
///     <description>
///       <c>removeSuffixRegex</c>: applies each regex anchored to the end of the
///       transcription (case-insensitive) and removes the matched suffix. Used for
///       hallucinated suffixes like "Titulky vytvořil JohnyX." appended by Whisper
///       to legitimate dictation as a leftover from its YouTube training data.
///     </description>
///   </item>
/// </list>
/// Patterns are loaded from the same JSON configuration file used by other filters
/// and are hot-reloaded when the file changes.
/// </summary>
public class WhisperHallucinationFilterStrategy : ITextFilterStrategy
{
    private readonly ILogger<WhisperHallucinationFilterStrategy> _logger;
    private readonly string? _configPath;
    private List<string> _wholeTextPatterns = new();
    private List<Regex> _suffixRegexes = new();
    private DateTime _lastModified;
    private readonly object _lock = new();

    public WhisperHallucinationFilterStrategy(
        ILogger<WhisperHallucinationFilterStrategy> logger,
        string? configPath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configPath = configPath;

        if (!string.IsNullOrWhiteSpace(_configPath))
        {
            LoadPatterns();
        }
        else
        {
            _logger.LogInformation("Whisper hallucination filter disabled (no config path)");
        }
    }

    /// <inheritdoc/>
    public string Name => "Whisper Hallucinations (whole-text + suffix regex)";

    /// <inheritdoc/>
    public bool IsEnabled => _wholeTextPatterns.Count > 0 || _suffixRegexes.Count > 0;

    /// <inheritdoc/>
    public string Apply(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        CheckForUpdates();

        if (_wholeTextPatterns.Count == 0 && _suffixRegexes.Count == 0)
            return text;

        // Snapshot pattern lists under lock to avoid races with hot reload.
        List<string> wholeTextPatterns;
        List<Regex> suffixRegexes;
        lock (_lock)
        {
            wholeTextPatterns = _wholeTextPatterns;
            suffixRegexes = _suffixRegexes;
        }

        var trimmed = text.Trim();

        // Whole-text exact match removal: if the entire transcription is just a hallucination,
        // wipe it. The downstream pipeline (e.g. DictationWorker) treats empty text as "no result".
        foreach (var pattern in wholeTextPatterns)
        {
            if (string.Equals(trimmed, pattern, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Removed whole-text hallucination: '{Pattern}'", pattern);
                return string.Empty;
            }
        }

        // Suffix regex removal: strip hallucinated suffixes while preserving the prefix.
        // Loop until no regex matches anymore so chained suffixes (e.g. "...JohnyX. Konec.")
        // are stripped layer by layer regardless of declaration order. Bounded to prevent
        // pathological infinite loops if a malformed regex matches its own replacement.
        var result = text;
        const int maxIterations = 10;
        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            var changedThisPass = false;
            foreach (var regex in suffixRegexes)
            {
                var stripped = regex.Replace(result, string.Empty);
                if (stripped.Length != result.Length)
                {
                    _logger.LogDebug(
                        "Removed suffix hallucination via /{Pattern}/: '{Before}' → '{After}'",
                        regex,
                        result,
                        stripped);
                    result = stripped;
                    changedThisPass = true;
                }
            }

            if (!changedThisPass)
            {
                break;
            }
        }

        return result;
    }

    private void LoadPatterns()
    {
        if (string.IsNullOrWhiteSpace(_configPath))
            return;

        try
        {
            if (!File.Exists(_configPath))
            {
                _logger.LogWarning("Text filters config not found: {Path}", _configPath);
                return;
            }

            var fileInfo = new FileInfo(_configPath);

            lock (_lock)
            {
                _lastModified = fileInfo.LastWriteTimeUtc;

                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<TextFiltersConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (config != null)
                {
                    _wholeTextPatterns = config.RemoveWholeText
                        ?.Where(p => !string.IsNullOrWhiteSpace(p))
                        .Select(p => p.Trim())
                        .ToList() ?? new();

                    _suffixRegexes = config.RemoveSuffixRegex
                        ?.Where(p => !string.IsNullOrWhiteSpace(p))
                        .Select(p => CompileSuffixRegex(p))
                        .Where(r => r != null)
                        .Cast<Regex>()
                        .ToList() ?? new();

                    _logger.LogInformation(
                        "Loaded {WholeText} whole-text and {SuffixRegex} suffix-regex hallucination patterns from {Path}",
                        _wholeTextPatterns.Count,
                        _suffixRegexes.Count,
                        _configPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Whisper hallucination patterns from {Path}", _configPath);
        }
    }

    private Regex? CompileSuffixRegex(string pattern)
    {
        try
        {
            // Anchor to end of text. We do not anchor to start: the suffix can begin anywhere.
            // Caller-provided patterns should NOT include the trailing $ themselves; we add it.
            var anchored = pattern.EndsWith("$", StringComparison.Ordinal) ? pattern : pattern + "$";
            return new Regex(anchored, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid suffix regex pattern: '{Pattern}'", pattern);
            return null;
        }
    }

    private void CheckForUpdates()
    {
        if (string.IsNullOrWhiteSpace(_configPath) || !File.Exists(_configPath))
            return;

        try
        {
            var fileInfo = new FileInfo(_configPath);
            if (fileInfo.LastWriteTimeUtc > _lastModified)
            {
                _logger.LogInformation("Text filters config changed, reloading hallucination patterns...");
                LoadPatterns();
            }
        }
        catch
        {
            // Ignore file access errors during check
        }
    }
}
