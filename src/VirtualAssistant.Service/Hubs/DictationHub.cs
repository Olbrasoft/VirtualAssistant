using Microsoft.AspNetCore.SignalR;
using Olbrasoft.VirtualAssistant.Service.Hubs.Services;

namespace Olbrasoft.VirtualAssistant.Service.Hubs;

/// <summary>
/// SignalR hub for remote dictation control from mobile devices. After the
/// #970 split this class is a thin wire-protocol facade — the three
/// underlying domains (recording/keyboard, desktop/workspace, screenshots)
/// live behind their own focused command services in
/// <c>Hubs/Services/</c>. Method names are preserved verbatim so the
/// Remote Control JavaScript client and unit tests keep working without
/// any client-side changes.
/// </summary>
public class DictationHub : Hub
{
    private readonly IRemoteRecordingCommands _recording;
    private readonly IRemoteDesktopCommands _desktop;
    private readonly IRemoteScreenshotCommands _screenshot;
    private readonly ILogger<DictationHub> _logger;

    public DictationHub(
        IRemoteRecordingCommands recording,
        IRemoteDesktopCommands desktop,
        IRemoteScreenshotCommands screenshot,
        ILogger<DictationHub> logger)
    {
        _recording = recording ?? throw new ArgumentNullException(nameof(recording));
        _desktop = desktop ?? throw new ArgumentNullException(nameof(desktop));
        _screenshot = screenshot ?? throw new ArgumentNullException(nameof(screenshot));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    // --- Desktop / window domain -------------------------------------------

    public Task<string> GetFocusedApp() => _desktop.GetFocusedAppAsync();

    public Task<bool> CloseApp(string wmClass) => _desktop.CloseAppAsync(wmClass);

    public Task<WorkspaceInfo> GetWorkspaceInfo() => _desktop.GetWorkspaceInfoAsync();

    public Task SwitchWorkspace(int workspaceNumber) => _desktop.SwitchWorkspaceAsync(workspaceNumber);

    public Task<bool> ActivateApp(string wmClass) => _desktop.ActivateAppAsync(wmClass);

    // --- Recording / keyboard / clipboard domain ---------------------------

    public Task<StatusResponse> GetStatus() => _recording.GetStatusAsync();

    public Task ToggleRecording() => _recording.ToggleRecordingAsync();

    public Task ToggleQuickRecording() => _recording.ToggleQuickRecordingAsync();

    public Task StartDictation() => _recording.StartDictationAsync();

    public Task StopDictationWithMode(bool quick) => _recording.StopDictationWithModeAsync(quick);

    public Task CancelTranscription() => _recording.CancelTranscriptionAsync();

    public Task PressEnter() => _recording.PressEnterAsync();

    public Task<bool> SendContinue() => _recording.SendContinueAsync();

    public Task<string> GetActiveCliApp() => _recording.GetActiveCliAppAsync();

    public Task PasteFromClipboard() => _recording.PasteFromClipboardAsync();

    public Task<bool> PasteTranscription(string text) => _recording.PasteTranscriptionAsync(text);

    public Task ClearText() => _recording.ClearTextAsync();

    // --- Screenshot domain -------------------------------------------------

    public Task<bool> IsScreenshotAvailable() => _screenshot.IsScreenshotAvailableAsync();

    public Task InsertScreenshotPath() => _screenshot.InsertScreenshotPathAsync();
}

/// <summary>
/// Response model for status queries.
/// </summary>
public class StatusResponse
{
    public bool IsRecording { get; set; }
    public bool IsTranscribing { get; set; }
}

/// <summary>
/// Event types for dictation notifications.
/// </summary>
public enum DictationEventType
{
    RecordingStarted = 0,
    RecordingStopped = 1,
    TranscriptionStarted = 2,
    TranscriptionCompleted = 3,
    RawTranscriptionCompleted = 4,
    QuickTranscriptionCompleted = 5
}

/// <summary>
/// Event model sent to SignalR clients.
/// </summary>
public class DictationEvent
{
    public DictationEventType EventType { get; set; }
    public string? Text { get; set; }
}

/// <summary>
/// Response model for workspace info queries.
/// </summary>
public class WorkspaceInfo
{
    public int CurrentWorkspace { get; set; }
    public int TotalWorkspaces { get; set; }
}
