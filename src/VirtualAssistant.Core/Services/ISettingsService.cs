namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Service for managing persistent application settings stored in JSON file.
/// Settings are stored at ~/.config/virtual-assistant/settings.json
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets a setting value by key, or returns the default value if not found.
    /// </summary>
    /// <typeparam name="T">Type of the setting value</typeparam>
    /// <param name="key">Setting key (e.g., "tts.muted")</param>
    /// <param name="defaultValue">Default value if key not found</param>
    /// <returns>Setting value or default value</returns>
    Task<T?> GetAsync<T>(string key, T? defaultValue = default);

    /// <summary>
    /// Sets a setting value by key.
    /// </summary>
    /// <typeparam name="T">Type of the setting value</typeparam>
    /// <param name="key">Setting key (e.g., "tts.muted")</param>
    /// <param name="value">Value to store</param>
    Task SetAsync<T>(string key, T value);
}
