using Microsoft.AspNetCore.SignalR;
using Olbrasoft.LinuxDesktop.Core.Services;
using Olbrasoft.VirtualAssistant.Core.Keyboard;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.StateMachine;

namespace Olbrasoft.VirtualAssistant.Service.Hubs;

/// <summary>
/// SignalR hub for remote dictation control from mobile devices.
/// </summary>
public class DictationHub : Hub
{
    private static readonly HashSet<string> AllowedApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "discord", "ferdium"
    };

    private readonly IDictationService _dictationService;
    private readonly IKeyboardSimulationService _keyboardSimulation;
    private readonly IWindowQueryService _windowQuery;
    private readonly IWindowActionService _windowAction;
    private readonly ILogger<DictationHub> _logger;

    public DictationHub(
        IDictationService dictationService,
        IKeyboardSimulationService keyboardSimulation,
        IWindowQueryService windowQuery,
        IWindowActionService windowAction,
        ILogger<DictationHub> logger)
    {
        _dictationService = dictationService;
        _keyboardSimulation = keyboardSimulation;
        _windowQuery = windowQuery;
        _windowAction = windowAction;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Gets the current dictation status.
    /// </summary>
    public Task<StatusResponse> GetStatus()
    {
        return Task.FromResult(new StatusResponse
        {
            IsRecording = _dictationService.State == DictationState.Recording,
            IsTranscribing = _dictationService.State == DictationState.Transcribing
        });
    }

    /// <summary>
    /// Toggles dictation state (idle -> recording, recording -> transcribing).
    /// </summary>
    public async Task ToggleRecording()
    {
        if (_dictationService.State == DictationState.Idle)
        {
            try { await _dictationService.StartDictationAsync(); }
            catch (Exception ex) { _logger.LogError(ex, "StartDictation failed"); }
        }
        else if (_dictationService.State == DictationState.Recording)
        {
            try { await _dictationService.StopDictationAsync(); }
            catch (Exception ex) { _logger.LogError(ex, "StopDictation failed"); }
        }
    }

    /// <summary>
    /// Cancels ongoing transcription.
    /// </summary>
    public Task CancelTranscription()
    {
        _logger.LogInformation("CancelTranscription called from client {ConnectionId}", Context.ConnectionId);
        _dictationService.CancelTranscription();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends Enter key press to active window.
    /// </summary>
    public async Task PressEnter()
    {
        _logger.LogInformation("PressEnter called from client {ConnectionId}", Context.ConnectionId);
        try { await _keyboardSimulation.SendKeyAsync("enter"); }
        catch (Exception ex) { _logger.LogError(ex, "PressEnter failed"); }
    }

    /// <summary>
    /// Sends Ctrl+U key press to clear current line in terminal.
    /// </summary>
    public async Task ClearText()
    {
        _logger.LogInformation("ClearText called from client {ConnectionId}", Context.ConnectionId);
        try { await _keyboardSimulation.SendKeyAsync("ctrl+u"); }
        catch (Exception ex) { _logger.LogError(ex, "ClearText failed"); }
    }

    /// <summary>
    /// Activates a desktop application window by WM class name.
    /// </summary>
    public async Task<bool> ActivateApp(string wmClass)
    {
        if (string.IsNullOrWhiteSpace(wmClass) || !AllowedApps.Contains(wmClass))
        {
            _logger.LogWarning("ActivateApp rejected: '{WmClass}' is not in allowlist", wmClass);
            return false;
        }

        try
        {
            _logger.LogInformation("ActivateApp '{WmClass}' from client {ConnectionId}", wmClass, Context.ConnectionId);

            var windows = await _windowQuery.GetWindowsAsync();
            var window = windows.FirstOrDefault(w =>
                string.Equals(w.WmClass, wmClass, StringComparison.OrdinalIgnoreCase));

            if (window == null)
            {
                _logger.LogWarning("No window found with WM class '{WmClass}'", wmClass);
                return false;
            }

            await _windowAction.ActivateWindowAsync(window.Id);
            _logger.LogInformation("Activated window '{Title}' (ID: {Id})", window.Title, window.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ActivateApp '{WmClass}' failed", wmClass);
            return false;
        }
    }
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
    TranscriptionCompleted = 3
}

/// <summary>
/// Event model sent to SignalR clients.
/// </summary>
public class DictationEvent
{
    public DictationEventType EventType { get; set; }
    public string? Text { get; set; }
}
