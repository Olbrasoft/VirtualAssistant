namespace Olbrasoft.VirtualAssistant.Voice.Configuration;

/// <summary>
/// Configuration options for Google Speech-to-Text API.
/// Uses the Chromium Speech API endpoint.
/// </summary>
public class GoogleSpeechToTextOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "GoogleSpeechToText";

    /// <summary>
    /// Google Speech API key (Chromium key).
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Language code for transcription (e.g., "cs-CZ", "en-US").
    /// </summary>
    public string Language { get; set; } = "cs-CZ";

    /// <summary>
    /// API timeout in milliseconds.
    /// </summary>
    public int TimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Enable or disable Google Speech-to-Text.
    /// When disabled, falls back to Whisper.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
