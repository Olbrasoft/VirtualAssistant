using Olbrasoft.VirtualAssistant.Core.Enums;

namespace Olbrasoft.VirtualAssistant.Core.Exceptions;

/// <summary>
/// Exception thrown when an LLM provider is rate limited.
/// </summary>
public class RateLimitException : Exception
{
    /// <summary>
    /// The provider that was rate limited.
    /// </summary>
    public LlmProvider Provider { get; }
    
    /// <summary>
    /// When the rate limit resets (if known).
    /// </summary>
    public DateTime? ResetAt { get; }

    public RateLimitException(LlmProvider provider, string message, DateTime? resetAt = null)
        : base(message)
    {
        Provider = provider;
        ResetAt = resetAt;
    }
}
