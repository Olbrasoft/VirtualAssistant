namespace VirtualAssistant.LlmChain.Exceptions;

/// <summary>
/// Exception thrown when a provider returns 429 (rate limited).
/// </summary>
public class RateLimitedException : Exception
{
    public string ProviderName { get; }
    public DateTime? ResetAt { get; }

    public RateLimitedException(string providerName, DateTime? resetAt = null)
        : base($"Provider {providerName} rate limited")
    {
        ProviderName = providerName;
        ResetAt = resetAt;
    }
}
