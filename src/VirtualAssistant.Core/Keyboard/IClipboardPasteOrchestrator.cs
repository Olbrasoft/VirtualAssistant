namespace Olbrasoft.VirtualAssistant.Core.Keyboard;

/// <summary>
/// Encapsulates the "save current selection → stage new text → run paste → restore"
/// choreography shared between <c>TypeIntoActiveWindow</c> and <c>PasteFromClipboard</c>.
/// The orchestrator owns the save/restore contract; the caller supplies the actual
/// paste action (a dotool key press, almost always).
/// </summary>
public interface IClipboardPasteOrchestrator
{
    /// <summary>
    /// Saves the current content of the selection indicated by <paramref name="usePrimary"/>,
    /// replaces it with <paramref name="stagedText"/>, runs <paramref name="performPasteAsync"/>,
    /// and restores the original selection in a finally block.
    /// </summary>
    /// <remarks>
    /// If the original selection is empty, the restore step is skipped (matching the
    /// legacy behavior — no point overwriting a blank selection with blank).
    /// Failures during the restore step are logged and swallowed; they must not
    /// mask the caller's real result.
    /// </remarks>
    Task<bool> StageAndRestoreAsync(
        string stagedText,
        bool usePrimary,
        Func<Task<bool>> performPasteAsync,
        CancellationToken cancellationToken);
}
