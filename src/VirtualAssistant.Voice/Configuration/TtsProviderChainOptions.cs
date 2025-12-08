namespace Olbrasoft.VirtualAssistant.Voice.Configuration;

/// <summary>
/// Configuration options for TTS provider chain with circuit breaker.
/// </summary>
public sealed class TtsProviderChainOptions
{
    public const string SectionName = "TtsProviderChain";

    /// <summary>
    /// Ordered list of provider names to try (e.g., "EdgeTTS-HTTP", "GoogleTTS", "VoiceRSS", "PiperTTS").
    /// Providers are tried in order until one succeeds.
    /// Default order: Edge (fastest) → Google (female) → VoiceRSS (male, rate limited) → Piper (offline).
    /// </summary>
    public List<string> Providers { get; set; } = [];

    /// <summary>
    /// Circuit breaker settings.
    /// </summary>
    public CircuitBreakerOptions CircuitBreaker { get; set; } = new();
}

/// <summary>
/// Circuit breaker configuration for TTS providers.
/// </summary>
public sealed class CircuitBreakerOptions
{
    /// <summary>
    /// Initial cooldown period in minutes before retrying a failed provider.
    /// Default: 5 minutes.
    /// </summary>
    public int CooldownMinutes { get; set; } = 5;

    /// <summary>
    /// Maximum cooldown period in minutes when using exponential backoff.
    /// Default: 60 minutes.
    /// </summary>
    public int MaxCooldownMinutes { get; set; } = 60;

    /// <summary>
    /// Use exponential backoff for consecutive failures.
    /// If true, cooldown doubles with each failure up to MaxCooldownMinutes.
    /// Default: true.
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;
}

/// <summary>
/// Configuration for VoiceRSS TTS provider.
/// </summary>
public sealed class VoiceRssOptions
{
    public const string SectionName = "VoiceRss";

    /// <summary>
    /// Path to file containing API key, or the API key directly.
    /// Supports ~ for home directory (e.g., "~/Dokumenty/credentials/voicerss.txt").
    /// </summary>
    public string ApiKeyFile { get; set; } = "~/Dokumenty/credentials/voicerss.txt";

    /// <summary>
    /// Language code (e.g., "cs-cz" for Czech).
    /// </summary>
    public string Language { get; set; } = "cs-cz";

    /// <summary>
    /// Voice name (e.g., "Josef" for Czech male voice).
    /// </summary>
    public string Voice { get; set; } = "Josef";

    /// <summary>
    /// Speech speed (-10 to 10, default 0). Positive = faster.
    /// </summary>
    public int Speed { get; set; } = 3;

    /// <summary>
    /// Audio format (e.g., "44khz_16bit_mono").
    /// </summary>
    public string AudioFormat { get; set; } = "44khz_16bit_mono";

    /// <summary>
    /// Audio codec (e.g., "MP3", "WAV", "OGG").
    /// </summary>
    public string AudioCodec { get; set; } = "MP3";
}

/// <summary>
/// Configuration for Google TTS (gTTS) provider.
/// </summary>
public sealed class GoogleTtsOptions
{
    public const string SectionName = "GoogleTts";

    /// <summary>
    /// Language code for gTTS (e.g., "cs" for Czech).
    /// </summary>
    public string Language { get; set; } = "cs";

    /// <summary>
    /// Slow mode (speaks more slowly). Default: false.
    /// </summary>
    public bool Slow { get; set; } = false;
}
