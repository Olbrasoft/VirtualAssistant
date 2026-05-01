namespace Olbrasoft.VirtualAssistant.Data.Entities;

/// <summary>
/// Persisted per-key state for the Google Cloud TTS multi-key provider.
/// One row per configured API key (identified by display name). The provider
/// reads this on startup to restore parked state and counters across restarts,
/// and writes it back via <c>IApiKeyUsageStore</c> after every synthesis.
/// </summary>
public class TtsKeyUsage
{
    public int Id { get; set; }

    /// <summary>Display name of the key (e.g. "primary", "fallback-1") - unique.</summary>
    public required string KeyName { get; set; }

    /// <summary>UTC year of the current monthly counter window.</summary>
    public int CounterYear { get; set; }

    /// <summary>UTC month (1-12) of the current monthly counter window.</summary>
    public int CounterMonth { get; set; }

    /// <summary>Characters synthesized in the current UTC month.</summary>
    public long MonthlyCharacterCount { get; set; }

    /// <summary>Lifetime successful synthesis count.</summary>
    public long TotalSuccesses { get; set; }

    /// <summary>Lifetime failed synthesis count.</summary>
    public long TotalFailures { get; set; }

    /// <summary>Number of consecutive failures since the last success.</summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>UTC timestamp of the last successful call (null if never).</summary>
    public DateTime? LastSuccessUtc { get; set; }

    /// <summary>UTC timestamp of the last failed call (null if never).</summary>
    public DateTime? LastErrorUtc { get; set; }

    /// <summary>
    /// Reason of the last error - HTTP status code or sentinel like
    /// "MonthlyLimitExceeded", "HttpRequestException".
    /// </summary>
    public string? LastErrorReason { get; set; }

    /// <summary>
    /// Routing state mirrored from <c>ApiKeyState</c> in the provider library.
    /// Stored as int to keep migrations stable when new states are added upstream.
    /// </summary>
    public int State { get; set; }

    /// <summary>UTC time until which the key is parked (null if available).</summary>
    public DateTime? CooldownUntilUtc { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
