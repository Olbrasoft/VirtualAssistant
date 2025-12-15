namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Status of a TTS provider in the circuit breaker system.
/// </summary>
public sealed record TtsProviderStatus(
    string Name,
    bool IsHealthy,
    DateTime? LastFailure,
    DateTime? NextRetryAt,
    int ConsecutiveFailures
);

/// <summary>
/// Interface for TTS provider chain with fallback support.
/// </summary>
public interface ITtsProviderChain
{
    /// <summary>
    /// Synthesizes text to audio using the provider chain.
    /// Tries each provider in order until one succeeds.
    /// </summary>
    Task<(byte[]? Audio, string? ProviderUsed)> SynthesizeAsync(
        string text,
        VoiceConfig config,
        string? sourceProfile = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of all configured providers.
    /// </summary>
    IReadOnlyList<TtsProviderStatus> GetProvidersStatus();

    /// <summary>
    /// Resets the circuit breaker state for a specific provider or all providers.
    /// </summary>
    void ResetCircuitBreaker(string? providerName = null);
}
