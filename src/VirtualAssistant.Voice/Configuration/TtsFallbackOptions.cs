namespace Olbrasoft.VirtualAssistant.Voice.Configuration;

/// <summary>
/// Configuration options for TTS fallback behavior.
/// </summary>
public sealed class TtsFallbackOptions
{
    public const string SectionName = "TtsFallback";

    /// <summary>
    /// Enable automatic fallback to Piper when Edge TTS is unavailable.
    /// </summary>
    public bool EnableFallback { get; set; } = true;

    /// <summary>
    /// Check location/VPN status to proactively switch to fallback.
    /// </summary>
    public bool CheckLocation { get; set; } = true;

    /// <summary>
    /// If true, silently skip TTS when all providers fail instead of throwing.
    /// </summary>
    public bool SilentOnFailure { get; set; } = true;

    /// <summary>
    /// Check Edge TTS availability before each request.
    /// If false, only checks on startup or after failures.
    /// </summary>
    public bool AlwaysCheckAvailability { get; set; } = false;
}
