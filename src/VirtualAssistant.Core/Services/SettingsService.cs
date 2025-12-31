using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Service for managing persistent application settings stored in JSON file.
/// Settings are stored at ~/.config/virtual-assistant/settings.json
/// Thread-safe implementation using SemaphoreSlim.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly string _settingsPath;
    private readonly ILogger<SettingsService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config/virtual-assistant");

        Directory.CreateDirectory(configDir);
        _settingsPath = Path.Combine(configDir, "settings.json");

        _logger.LogDebug("SettingsService initialized with path: {Path}", _settingsPath);
    }

    /// <summary>
    /// Gets a setting value by key, or returns the default value if not found.
    /// </summary>
    public async Task<T?> GetAsync<T>(string key, T? defaultValue = default)
    {
        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(_settingsPath))
            {
                _logger.LogDebug("Settings file does not exist, returning default value for key: {Key}", key);
                return defaultValue;
            }

            var json = await File.ReadAllTextAsync(_settingsPath);
            var settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

            if (settings?.TryGetValue(key, out var value) == true)
            {
                var deserializedValue = value.Deserialize<T>();
                _logger.LogDebug("Retrieved setting {Key} = {Value}", key, deserializedValue);
                return deserializedValue;
            }

            _logger.LogDebug("Setting {Key} not found, returning default value", key);
            return defaultValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get setting {Key}, returning default value", key);
            return defaultValue;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Sets a setting value by key.
    /// </summary>
    public async Task SetAsync<T>(string key, T value)
    {
        await _lock.WaitAsync();
        try
        {
            var settings = new Dictionary<string, object>();

            if (File.Exists(_settingsPath))
            {
                var json = await File.ReadAllTextAsync(_settingsPath);
                settings = JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                    ?? new Dictionary<string, object>();
            }

            settings[key] = value!;

            var newJson = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_settingsPath, newJson);
            _logger.LogDebug("Saved setting {Key} = {Value}", key, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set setting {Key}", key);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }
}
