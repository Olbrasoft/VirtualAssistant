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
/// Context information for TTS synthesis request.
/// Includes agent identification and provider chain configuration.
/// </summary>
public sealed record TtsContext
{
    /// <summary>
    /// Gets the agent/program name (e.g., "Claude Code", "Gemini").
    /// </summary>
    public string? AgentName { get; init; }

    /// <summary>
    /// Gets the unique identifier for this agent instance.
    /// When multiple instances of the same agent are running, each has a unique ID.
    /// </summary>
    public string? AgentInstanceId { get; init; }

    /// <summary>
    /// Gets the ordered list of provider names to try.
    /// When specified, overrides the default provider chain from configuration.
    /// </summary>
    public IReadOnlyList<string>? ProviderFallbackChain { get; init; }

    /// <summary>
    /// Gets the voice name to use for this request.
    /// Overrides default voice from VoiceConfig.
    /// </summary>
    public string? Voice { get; init; }

    /// <summary>
    /// Gets the legacy source profile (for backward compatibility).
    /// Prefer using ProviderFallbackChain for new code.
    /// </summary>
    public string? SourceProfile { get; init; }
}

/// <summary>
/// Interface for TTS provider chain with fallback support.
/// </summary>
public interface ITtsProviderChain
{
    /// <summary>
    /// Synthesizes text to audio using the provider chain.
    /// Tries each provider in order until one succeeds.
    /// Legacy method - uses sourceProfile for provider selection.
    /// </summary>
    Task<(byte[]? Audio, string? ProviderUsed)> SynthesizeAsync(
        string text,
        VoiceConfig config,
        string? sourceProfile = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Synthesizes text to audio using the provider chain with advanced options.
    /// Supports agent identification, custom provider chains, and voice selection.
    /// </summary>
    Task<(byte[]? Audio, string? ProviderUsed)> SynthesizeAdvancedAsync(
        string text,
        VoiceConfig config,
        TtsContext context,
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
