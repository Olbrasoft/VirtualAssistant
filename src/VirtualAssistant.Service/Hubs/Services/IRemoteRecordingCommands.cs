using Olbrasoft.VirtualAssistant.Service.Hubs;

namespace Olbrasoft.VirtualAssistant.Service.Hubs.Services;

/// <summary>
/// Dictation / keyboard / clipboard slice of the Remote Control hub (#970
/// split). Focused on the transcription flow and the keyboard helpers used
/// while typing — no desktop-window, workspace, or screenshot concerns.
/// </summary>
public interface IRemoteRecordingCommands
{
    Task<StatusResponse> GetStatusAsync();
    Task ToggleRecordingAsync();
    Task ToggleQuickRecordingAsync();
    Task StartDictationAsync();
    Task StopDictationWithModeAsync(bool quick);
    Task CancelTranscriptionAsync();
    Task PressEnterAsync();
    Task<bool> SendContinueAsync();
    Task<string> GetActiveCliAppAsync();
    Task PasteFromClipboardAsync();
    Task<bool> PasteTranscriptionAsync(string text);
    Task ClearTextAsync();
}
