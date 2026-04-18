using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Core.Keyboard;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;
using Olbrasoft.VirtualAssistant.Data.Queries.PromptQueries;
using Olbrasoft.VirtualAssistant.Service.Hubs;
using IDesktopContextService = Olbrasoft.VirtualAssistant.Core.Services.IDesktopContextService;

namespace Olbrasoft.VirtualAssistant.Service.Hubs.Services;

/// <inheritdoc />
public class RemoteRecordingCommands : IRemoteRecordingCommands
{
    private const int MaxPasteLength = 10000;

    private readonly ILogger<RemoteRecordingCommands> _logger;
    private readonly IDictationService _dictationService;
    private readonly IKeyboardSimulationService _keyboardSimulation;
    private readonly IDesktopContextService _desktopContext;
    private readonly ICliAppDetector _cliAppDetector;
    private readonly ITerminalDetector _terminalDetector;
    private readonly IQueryProcessor _queryProcessor;

    public RemoteRecordingCommands(
        ILogger<RemoteRecordingCommands> logger,
        IDictationService dictationService,
        IKeyboardSimulationService keyboardSimulation,
        IDesktopContextService desktopContext,
        ICliAppDetector cliAppDetector,
        ITerminalDetector terminalDetector,
        IQueryProcessor queryProcessor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dictationService = dictationService ?? throw new ArgumentNullException(nameof(dictationService));
        _keyboardSimulation = keyboardSimulation ?? throw new ArgumentNullException(nameof(keyboardSimulation));
        _desktopContext = desktopContext ?? throw new ArgumentNullException(nameof(desktopContext));
        _cliAppDetector = cliAppDetector ?? throw new ArgumentNullException(nameof(cliAppDetector));
        _terminalDetector = terminalDetector ?? throw new ArgumentNullException(nameof(terminalDetector));
        _queryProcessor = queryProcessor ?? throw new ArgumentNullException(nameof(queryProcessor));
    }

    public Task<StatusResponse> GetStatusAsync() =>
        Task.FromResult(new StatusResponse
        {
            IsRecording = _dictationService.State == DictationState.Recording,
            IsTranscribing = _dictationService.State == DictationState.Transcribing
        });

    public async Task ToggleRecordingAsync()
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

    public async Task ToggleQuickRecordingAsync()
    {
        if (_dictationService.State == DictationState.Idle)
        {
            try { await _dictationService.StartQuickDictationAsync(); }
            catch (Exception ex) { _logger.LogError(ex, "StartQuickDictation failed"); }
        }
        else if (_dictationService.State == DictationState.Recording)
        {
            try { await _dictationService.StopDictationAsync(); }
            catch (Exception ex) { _logger.LogError(ex, "StopDictation (quick) failed"); }
        }
    }

    public async Task StartDictationAsync()
    {
        if (_dictationService.State == DictationState.Idle)
        {
            try { await _dictationService.StartDictationAsync(); }
            catch (Exception ex) { _logger.LogError(ex, "StartDictation failed"); }
        }
    }

    public async Task StopDictationWithModeAsync(bool quick)
    {
        if (_dictationService.State == DictationState.Recording)
        {
            try { await _dictationService.StopDictationAsync(quick); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StopDictationWithMode(quick={Quick}) failed", quick);
            }
        }
    }

    public Task CancelTranscriptionAsync()
    {
        _logger.LogInformation("CancelTranscription");
        _dictationService.CancelTranscription();
        return Task.CompletedTask;
    }

    public async Task PressEnterAsync()
    {
        _logger.LogInformation("PressEnter");
        try { await _keyboardSimulation.SendKeyAsync("enter"); }
        catch (Exception ex) { _logger.LogError(ex, "PressEnter failed"); }
    }

    public async Task<bool> SendContinueAsync()
    {
        _logger.LogInformation("SendContinue");
        try
        {
            var activeApp = await GetActiveCliAppAsync();
            if (!string.Equals(activeApp, "Claude Code", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("SendContinue rejected: active CLI app is '{App}', not Claude Code", activeApp);
                return false;
            }

            await _keyboardSimulation.TypeIntoActiveWindowAsync("Pokračuj");
            await _keyboardSimulation.SendKeyAsync("enter");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendContinue failed");
            return false;
        }
    }

    public async Task<string> GetActiveCliAppAsync()
    {
        try
        {
            var cliApp = await _cliAppDetector.DetectCliAppAsync();
            if (cliApp != null)
                return cliApp.AppName;

            var context = await _desktopContext.GetCurrentContextAsync();
            var prompt = await _queryProcessor.ProcessAsync(
                new GetPromptByAppIdPatternQuery(context.ActiveWindowTitle, context.ActiveApplication),
                CancellationToken.None);
            return prompt?.ApplicationName ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetActiveCliApp failed");
            return "";
        }
    }

    public async Task PasteFromClipboardAsync()
    {
        _logger.LogInformation("PasteFromClipboard");
        try { await _keyboardSimulation.PasteFromClipboardAsync(); }
        catch (Exception ex) { _logger.LogError(ex, "PasteFromClipboard failed"); }
    }

    public async Task<bool> PasteTranscriptionAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("PasteTranscription: empty text, ignoring");
            return false;
        }

        if (text.Length > MaxPasteLength)
        {
            _logger.LogWarning("PasteTranscription rejected: text length {Length} exceeds max {Max}", text.Length, MaxPasteLength);
            return false;
        }

        _logger.LogInformation("PasteTranscription {Length} chars", text.Length);
        try
        {
            await _keyboardSimulation.TypeIntoActiveWindowAsync(text);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PasteTranscription failed");
            return false;
        }
    }

    public async Task ClearTextAsync()
    {
        _logger.LogInformation("ClearText");
        try
        {
            var cliApp = await _cliAppDetector.DetectCliAppAsync();
            if (cliApp != null)
            {
                _logger.LogInformation("ClearText: CLI app '{App}' detected, sending End×10 + Ctrl+U×10", cliApp.AppName);
                var keys = new List<string>();
                for (var i = 0; i < 10; i++) keys.Add("end");
                for (var i = 0; i < 10; i++) keys.Add("ctrl+u");
                await _keyboardSimulation.SendKeySequenceAsync(keys);
                return;
            }

            var isTerminal = await _terminalDetector.IsTerminalActiveAsync();
            if (isTerminal)
            {
                _logger.LogInformation("ClearText: regular terminal, sending Ctrl+U");
                await _keyboardSimulation.SendKeyAsync("ctrl+u");
                return;
            }

            _logger.LogInformation("ClearText: GUI app, sending Ctrl+A + Delete");
            await _keyboardSimulation.SendKeySequenceAsync(["ctrl+a", "delete"]);
        }
        catch (Exception ex) { _logger.LogError(ex, "ClearText failed"); }
    }
}
