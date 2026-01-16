namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Factory for creating and selecting LLM providers.
/// </summary>
public interface ILlmProviderFactory
{
    /// <summary>
    /// Gets the currently active LLM provider.
    /// </summary>
    /// <returns>The active ILlmProvider instance.</returns>
    ILlmProvider GetActiveProvider();

    /// <summary>
    /// Gets a specific LLM provider by name.
    /// </summary>
    /// <param name="providerName">Name of the provider (e.g., "mistral", "zen").</param>
    /// <returns>The requested ILlmProvider instance, or null if not found.</returns>
    ILlmProvider? GetProvider(string providerName);

    /// <summary>
    /// Gets all available provider names.
    /// </summary>
    /// <returns>Collection of available provider names.</returns>
    IReadOnlyCollection<string> GetAvailableProviders();

    /// <summary>
    /// Sets the active provider at runtime.
    /// </summary>
    /// <param name="providerName">Name of the provider to activate.</param>
    /// <returns>True if provider was found and activated, false otherwise.</returns>
    bool SetActiveProvider(string providerName);
}
