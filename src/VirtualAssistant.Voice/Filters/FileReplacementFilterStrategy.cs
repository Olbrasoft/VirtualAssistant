using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Voice.Filters;

/// <summary>
/// Filter strategy that applies text replacements from JSON configuration file.
/// Supports hot reload when file changes.
/// </summary>
public class FileReplacementFilterStrategy : ITextFilterStrategy
{
    private readonly ILogger<FileReplacementFilterStrategy> _logger;
    private readonly string? _configPath;
    private Dictionary<string, string> _replacements = new();
    private DateTime _lastModified;
    private readonly object _lock = new();

    public FileReplacementFilterStrategy(
        ILogger<FileReplacementFilterStrategy> logger,
        string? configPath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configPath = configPath;

        if (!string.IsNullOrWhiteSpace(_configPath))
        {
            LoadReplacements();
        }
        else
        {
            _logger.LogInformation("File-based replacements disabled (no config path)");
        }
    }

    /// <inheritdoc/>
    public string Name => "File Replacements";

    /// <inheritdoc/>
    public bool IsEnabled => _replacements.Count > 0;

    /// <inheritdoc/>
    public string Apply(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        // Check for file changes (hot reload)
        CheckForUpdates();

        if (_replacements.Count == 0)
            return text;

        var result = text;

        lock (_lock)
        {
            foreach (var (incorrect, correct) in _replacements)
            {
                if (result.Contains(incorrect, StringComparison.OrdinalIgnoreCase))
                {
                    result = result.Replace(incorrect, correct, StringComparison.OrdinalIgnoreCase);
                    _logger.LogDebug("Applied file replacement: '{Incorrect}' → '{Correct}'", incorrect, correct);
                }
            }
        }

        return result;
    }

    private void LoadReplacements()
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
                    _replacements = config.Replace ?? new();

                    _logger.LogInformation(
                        "Loaded {Count} file replacements from {Path}",
                        _replacements.Count,
                        _configPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load replacements from {Path}", _configPath);
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
                _logger.LogInformation("Text filters config changed, reloading replacements...");
                LoadReplacements();
            }
        }
        catch
        {
            // Ignore file access errors during check
        }
    }
}
