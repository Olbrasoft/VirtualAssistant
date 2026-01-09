namespace Olbrasoft.VirtualAssistant.Service.Services;

/// <summary>
/// Result of a prompt synchronization operation.
/// </summary>
/// <param name="Success">Whether the sync completed successfully.</param>
/// <param name="FilesCopied">Number of files copied.</param>
/// <param name="FilesFailed">Number of files that failed to copy.</param>
/// <param name="Errors">List of error messages.</param>
public record PromptSyncResult(
    bool Success,
    int FilesCopied,
    int FilesFailed,
    IReadOnlyList<string> Errors
);

/// <summary>
/// Service for synchronizing LLM prompt files from source to deployment directory.
/// </summary>
public interface IPromptSyncService
{
    /// <summary>
    /// Checks if prompts need synchronization.
    /// Returns true if any source prompt is newer than deployed, if source has new files,
    /// or if target has obsolete files that were deleted from source.
    /// </summary>
    /// <returns>True if prompts need synchronization, false otherwise.</returns>
    bool ArePromptsOutOfSync();

    /// <summary>
    /// Synchronizes prompts from source to deployment directory.
    /// </summary>
    /// <returns>Result of the sync operation.</returns>
    PromptSyncResult SyncPrompts();

    /// <summary>
    /// Gets the source directory path for prompts.
    /// </summary>
    string SourcePath { get; }

    /// <summary>
    /// Gets the target (deployment) directory path for prompts.
    /// </summary>
    string TargetPath { get; }
}
