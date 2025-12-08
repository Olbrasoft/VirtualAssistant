namespace VirtualAssistant.Core.Services;

/// <summary>
/// Configuration options for Claude dispatch service.
/// </summary>
public class ClaudeDispatchOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "ClaudeDispatch";

    /// <summary>
    /// URL for TTS notifications.
    /// </summary>
    public string TtsNotifyUrl { get; set; } = "http://localhost:5055/api/tts/notify";

    /// <summary>
    /// Default working directory for Claude execution.
    /// Supports ~ for home directory.
    /// </summary>
    public string DefaultWorkingDirectory { get; set; } = "~/Olbrasoft/VirtualAssistant";

    /// <summary>
    /// Timeout in minutes for Claude execution.
    /// </summary>
    public int TimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Whether to send TTS notification on successful completion.
    /// </summary>
    public bool NotifyOnSuccess { get; set; } = true;

    /// <summary>
    /// Expands ~ to home directory in the working directory path.
    /// </summary>
    public string GetExpandedWorkingDirectory()
    {
        if (DefaultWorkingDirectory.StartsWith("~/"))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                DefaultWorkingDirectory[2..]);
        }
        return DefaultWorkingDirectory;
    }
}
