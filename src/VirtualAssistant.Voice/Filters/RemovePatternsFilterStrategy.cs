using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Voice.Filters;

/// <summary>
/// Filter strategy that removes unwanted patterns (e.g., Whisper hallucinations) from text.
/// Patterns are loaded from JSON configuration file and support hot reload.
/// </summary>
public class RemovePatternsFilterStrategy : ITextFilterStrategy
{
    private readonly ILogger<RemovePatternsFilterStrategy> _logger;
    private readonly string? _configPath;
    private List<string> _patterns = new();
    private DateTime _lastModified;
    private readonly object _lock = new();

    public RemovePatternsFilterStrategy(
        ILogger<RemovePatternsFilterStrategy> logger,
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
            _logger.LogInformation("Remove patterns filter disabled (no config path)");
        }
    }

    /// <inheritdoc/>
    public string Name => "Remove Patterns (Whisper Hallucinations)";

    /// <inheritdoc/>
    public bool IsEnabled => _patterns.Count > 0;

    /// <inheritdoc/>
    public string Apply(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        // Check for file changes (hot reload)
        CheckForUpdates();

        if (_patterns.Count == 0)
            return text;

        var result = text;

        lock (_lock)
        {
            foreach (var pattern in _patterns)
            {
                if (result.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    result = result.Replace(pattern, "", StringComparison.OrdinalIgnoreCase);
                    _logger.LogDebug("Removed pattern: '{Pattern}'", pattern);
                }
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
                    _patterns = config.Remove
                        ?.Where(p => !string.IsNullOrWhiteSpace(p))
                        .ToList() ?? new();

                    _logger.LogInformation(
                        "Loaded {Count} remove patterns from {Path}",
                        _patterns.Count,
                        _configPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load patterns from {Path}", _configPath);
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
                _logger.LogInformation("Text filters config changed, reloading patterns...");
                LoadPatterns();
            }
        }
        catch
        {
            // Ignore file access errors during check
        }
    }
}
