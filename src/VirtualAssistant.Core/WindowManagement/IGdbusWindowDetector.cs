namespace Olbrasoft.VirtualAssistant.Core.WindowManagement;

/// <summary>
/// Active-window info read from the GNOME <c>window-calls</c> extension
/// via <c>gdbus</c>.
/// </summary>
public readonly record struct FocusedWindowInfo(string WmClass, int Pid, string Title);

/// <summary>
/// Resolves the currently focused window by calling the GNOME Shell
/// <c>window-calls</c> extension over gdbus. Separated from the detector
/// so the (brittle) JSON parsing can be swapped or stubbed without
/// touching orchestration logic.
/// </summary>
public interface IGdbusWindowDetector
{
    /// <summary>
    /// Returns info about the currently focused window, or null when the
    /// gdbus call fails or no focused window is reported. Detection and
    /// parsing errors are swallowed — this is called on the dictation hot
    /// path and such failures must never propagate. Caller-requested
    /// cancellation is still honored and may propagate as an
    /// <see cref="OperationCanceledException"/>.
    /// </summary>
    Task<FocusedWindowInfo?> GetFocusedWindowInfoAsync(CancellationToken cancellationToken);
}
