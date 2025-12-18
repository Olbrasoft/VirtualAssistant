namespace Olbrasoft.VirtualAssistant.Voice.Configuration;

/// <summary>
/// Configuration settings for SpeechToText integration.
/// </summary>
public class SpeechToTextSettings
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "SpeechToText";

    /// <summary>
    /// Gets or sets the base URL of the SpeechToText application.
    /// Default: http://localhost:5050
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:5050";

    /// <summary>
    /// Gets or sets the timeout for status requests in milliseconds.
    /// Default: 1000ms (1 second)
    /// </summary>
    public int StatusTimeoutMs { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the polling interval when waiting for speech to finish, in milliseconds.
    /// Default: 500ms
    /// </summary>
    public int PollingIntervalMs { get; set; } = 500;
}
