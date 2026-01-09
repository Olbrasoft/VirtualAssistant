namespace Olbrasoft.VirtualAssistant.Service.Tray.Menu;

/// <summary>
/// Represents the synchronization status of LLM prompts.
/// </summary>
public enum PromptSyncStatus
{
    /// <summary>
    /// Initial state, sync status not yet determined.
    /// </summary>
    Unknown,

    /// <summary>
    /// Source and deployed prompts are in sync.
    /// </summary>
    InSync,

    /// <summary>
    /// Source prompts are newer than deployed prompts.
    /// </summary>
    OutOfSync,

    /// <summary>
    /// Last synchronization attempt failed.
    /// </summary>
    SyncFailed,

    /// <summary>
    /// Synchronization is currently in progress.
    /// </summary>
    Syncing
}
