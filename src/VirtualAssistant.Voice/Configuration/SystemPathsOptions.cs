namespace Olbrasoft.VirtualAssistant.Voice.Configuration;

/// <summary>
/// Configuration options for system file paths.
/// Allows all lock files and temp paths to be configured via appsettings.
/// </summary>
public class SystemPathsOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "SystemPaths";

    /// <summary>
    /// Path to the speech lock file. When this file exists, TTS should not speak.
    /// Used by SpeechLockService and DictationWorker.
    /// </summary>
    public string SpeechLockFile { get; set; } = "/tmp/speech-lock";

    /// <summary>
    /// Path to the VirtualAssistant.Service instance lock file.
    /// Prevents multiple instances from running.
    /// </summary>
    public string VirtualAssistantLockFile { get; set; } = "/tmp/virtual-assistant.lock";

    /// <summary>
    /// Path to the PushToTalk service instance lock file.
    /// Prevents multiple instances from running.
    /// </summary>
    public string PushToTalkLockFile { get; set; } = "/tmp/push-to-talk-dictation.lock";

    /// <summary>
    /// Path to the TTS cache directory.
    /// Supports ~ for home directory.
    /// </summary>
    public string TtsCacheDirectory { get; set; } = "~/.cache/virtual-assistant-tts";

    /// <summary>
    /// Expands ~ to home directory in the TTS cache path.
    /// </summary>
    public string GetExpandedTtsCacheDirectory()
    {
        if (TtsCacheDirectory.StartsWith("~/"))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                TtsCacheDirectory[2..]);
        }
        return TtsCacheDirectory;
    }
}
