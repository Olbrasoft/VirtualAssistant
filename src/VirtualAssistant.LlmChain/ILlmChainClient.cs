namespace VirtualAssistant.LlmChain;

/// <summary>
/// Interface for the LLM chain client that provides resilient LLM completions
/// with automatic provider failover and key rotation.
/// </summary>
public interface ILlmChainClient
{
    /// <summary>
    /// Sends a completion request through the provider chain.
    /// Automatically handles failover to next provider on errors.
    /// </summary>
    /// <param name="request">The completion request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The completion result with provider info.</returns>
    Task<LlmChainResult> CompleteAsync(LlmChainRequest request, CancellationToken ct = default);
}

/// <summary>
/// Request for LLM completion.
/// </summary>
public class LlmChainRequest
{
    /// <summary>
    /// System prompt (instructions for the LLM).
    /// </summary>
    public required string SystemPrompt { get; init; }

    /// <summary>
    /// User message to process.
    /// </summary>
    public required string UserMessage { get; init; }

    /// <summary>
    /// Temperature for generation (0.0-1.0). Default 0.3.
    /// </summary>
    public float Temperature { get; init; } = 0.3f;

    /// <summary>
    /// Maximum tokens to generate. Default 256.
    /// </summary>
    public int MaxTokens { get; init; } = 256;
}

/// <summary>
/// Result from LLM chain completion.
/// </summary>
public class LlmChainResult
{
    /// <summary>
    /// Whether the completion succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The generated content.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// Name of the provider that succeeded.
    /// </summary>
    public string? ProviderName { get; init; }

    /// <summary>
    /// API key identifier used (for debugging, shows first/last 4 chars).
    /// </summary>
    public string? KeyIdentifier { get; init; }

    /// <summary>
    /// Response time in milliseconds.
    /// </summary>
    public int ResponseTimeMs { get; init; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// List of providers tried and their errors.
    /// </summary>
    public List<ProviderAttempt> Attempts { get; init; } = [];

    /// <summary>
    /// Creates a success result.
    /// </summary>
    public static LlmChainResult Ok(string content, string provider, string keyId, int responseTimeMs, List<ProviderAttempt>? attempts = null) => new()
    {
        Success = true,
        Content = content,
        ProviderName = provider,
        KeyIdentifier = keyId,
        ResponseTimeMs = responseTimeMs,
        Attempts = attempts ?? []
    };

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    public static LlmChainResult Fail(string error, List<ProviderAttempt> attempts) => new()
    {
        Success = false,
        Error = error,
        Attempts = attempts
    };
}

/// <summary>
/// Record of a provider attempt.
/// </summary>
public class ProviderAttempt
{
    /// <summary>
    /// Provider name.
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Key identifier used.
    /// </summary>
    public required string KeyId { get; init; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Whether this was a rate limit error.
    /// </summary>
    public bool WasRateLimited { get; init; }

    /// <summary>
    /// Time of the attempt.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
