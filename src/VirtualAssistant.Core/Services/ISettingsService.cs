namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Service for managing persistent application settings.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets a setting value by key.
    /// </summary>
    /// <typeparam name="T">Type of the setting value.</typeparam>
    /// <param name="key">Setting key.</param>
    /// <param name="defaultValue">Default value if setting doesn't exist.</param>
    /// <returns>Setting value or default value.</returns>
    Task<T?> GetAsync<T>(string key, T? defaultValue = default);

    /// <summary>
    /// Sets a setting value by key.
    /// </summary>
    /// <typeparam name="T">Type of the setting value.</typeparam>
    /// <param name="key">Setting key.</param>
    /// <param name="value">Value to set.</param>
    Task SetAsync<T>(string key, T value);
}
