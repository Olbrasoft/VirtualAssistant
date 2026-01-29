namespace Olbrasoft.VirtualAssistant.Core.Speech;

/// <summary>
/// Factory for creating and managing speech transcriber instances.
/// Provides provider selection and database ID caching for tracking.
/// </summary>
public interface ISpeechTranscriberFactory
{
    /// <summary>
    /// Gets the currently active speech transcriber based on configuration.
    /// </summary>
    /// <returns>The configured primary speech transcriber.</returns>
    ISpeechTranscriber GetActiveProvider();

    /// <summary>
    /// Gets a specific provider by name.
    /// </summary>
    /// <param name="providerName">Provider name ("google" or "whisper").</param>
    /// <returns>The speech transcriber instance, or null if not found.</returns>
    ISpeechTranscriber? GetProvider(string providerName);

    /// <summary>
    /// Gets all available provider names.
    /// </summary>
    /// <returns>Collection of available provider names.</returns>
    IReadOnlyCollection<string> GetAvailableProviders();

    /// <summary>
    /// Gets the database provider ID for tracking.
    /// Returns from cache - loaded once at startup, no DB query per call.
    /// </summary>
    /// <param name="providerName">Provider name ("google" or "whisper").</param>
    /// <returns>Database provider ID.</returns>
    /// <exception cref="ArgumentException">If provider name is unknown.</exception>
    int GetProviderId(string providerName);
}
