namespace Olbrasoft.VirtualAssistant.Service.Configuration;

/// <summary>
/// Configuration options for prompt synchronization.
/// </summary>
public class PromptSyncOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "PromptSync";

    /// <summary>
    /// Path to the source directory containing prompt files.
    /// Supports ~ for home directory.
    /// </summary>
    public string SourcePath { get; set; } = "~/Olbrasoft/VirtualAssistant/src/VirtualAssistant.Voice/Prompts";

    /// <summary>
    /// Interval in seconds for checking if prompts are out of sync.
    /// Set to 0 to disable periodic checking.
    /// </summary>
    public int CheckIntervalSeconds { get; set; } = 30;
}
