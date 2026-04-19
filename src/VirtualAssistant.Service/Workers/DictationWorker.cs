using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Voice.StateMachine;
using Olbrasoft.VirtualAssistant.Service.Hubs;
using Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

namespace Olbrasoft.VirtualAssistant.Service.Workers;

/// <summary>
/// Background worker for keyboard-triggered dictation workflow.
/// Manages audio recording, transcription, and text insertion based on CapsLock state.
/// Uses dedicated audio capture instance (independent from continuous listening).
/// </summary>
public class DictationWorker : BackgroundService, IDictationControl, IDictationService
{
    private readonly ILogger<DictationWorker> _logger;
    private readonly IDictationKeyHandler _keyHandler;
    private readonly IDictationStateMachine _stateMachine;
    private readonly IDictationRecordingSession _recordingSession;
    private readonly IDictationCompletionPipeline _completionPipeline;
    private readonly IDictationStateBroadcaster _stateBroadcaster;
    private readonly IDictationCancellationCoordinator _cancellationCoordinator;

    private bool _dictationEnabled = true;
    private bool _quickDictationMode;
    private volatile bool _streamingTranscriptionEnabled;

    /// <inheritdoc/>
    public DictationState State => _stateMachine.CurrentState;

    /// <inheritdoc/>
    public event EventHandler<string>? TranscriptionCompleted;

    public DictationWorker(
        ILogger<DictationWorker> logger,
        IDictationKeyHandler keyHandler,
        IDictationStateMachine stateMachine,
        IDictationRecordingSession recordingSession,
        IDictationCompletionPipeline completionPipeline,
        IDictationStateBroadcaster stateBroadcaster,
        IDictationCancellationCoordinator cancellationCoordinator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _keyHandler = keyHandler ?? throw new ArgumentNullException(nameof(keyHandler));
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _recordingSession = recordingSession ?? throw new ArgumentNullException(nameof(recordingSession));
        _completionPipeline = completionPipeline ?? throw new ArgumentNullException(nameof(completionPipeline));
        _stateBroadcaster = stateBroadcaster ?? throw new ArgumentNullException(nameof(stateBroadcaster));
        _cancellationCoordinator = cancellationCoordinator ?? throw new ArgumentNullException(nameof(cancellationCoordinator));
    }

    /// <summary>
    /// Enables or disables dictation functionality.
    /// When disabled, CapsLock key events are ignored.
    /// If currently recording/transcribing, performs emergency stop.
    /// </summary>
    public void SetDictationEnabled(bool enabled)
    {
        _dictationEnabled = enabled;
        _logger.LogInformation("Dictation {Status}", enabled ? "enabled" : "disabled");

        // If disabling and currently recording/transcribing, emergency stop
        if (!enabled && _stateMachine.CurrentState != DictationState.Idle)
        {
            _logger.LogInformation("Dictation disabled while active - performing emergency stop");
            Task.Run(async () => await _cancellationCoordinator.EmergencyStopAsync());
        }
    }

    /// <summary>
    /// Enables or disables streaming (chunked) transcription for the fast dictation path.
    /// </summary>
    public void SetStreamingTranscriptionEnabled(bool enabled)
    {
        _streamingTranscriptionEnabled = enabled;
        _logger.LogInformation("Streaming transcription {Status}", enabled ? "enabled" : "disabled");
    }

    /// <inheritdoc/>
    public async Task StartDictationAsync()
    {
        if (!_dictationEnabled)
        {
            _logger.LogInformation("Dictation is disabled, ignoring start request");
            return;
        }

        if (_stateMachine.CurrentState == DictationState.Idle)
        {
            _quickDictationMode = false;
            await StartRecordingAsync();
        }
    }

    /// <inheritdoc/>
    public async Task StartQuickDictationAsync()
    {
        if (!_dictationEnabled)
        {
            _logger.LogInformation("Dictation is disabled, ignoring quick start request");
            return;
        }

        if (_stateMachine.CurrentState == DictationState.Idle)
        {
            _quickDictationMode = true;
            await StartRecordingAsync();
        }
    }

    /// <inheritdoc/>
    public Task StopDictationAsync()
    {
        if (_stateMachine.CurrentState == DictationState.Recording)
        {
            _ = Task.Run(async () => await StopAndTranscribeAsync());
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopDictationAsync(bool quickMode)
    {
        if (_stateMachine.CurrentState == DictationState.Recording)
        {
            // Override the mode that was chosen at start time. This is the
            // path used by the Remote Control's unified dictation button:
            // recording starts in normal mode by default, the user picks
            // fast vs slow only when releasing the button.
            _quickDictationMode = quickMode;
            _logger.LogInformation(
                "StopDictationAsync(quickMode={QuickMode}) - overriding start-time mode",
                quickMode);

            // If user picked slow mode but streaming was pre-transcribing chunks,
            // cancel those background tasks — they'd be discarded anyway, no point
            // burning GPU/serializing the Whisper semaphore for them.
            if (!quickMode && _recordingSession.IsStreamingActive)
            {
                _recordingSession.EndSession();
                _logger.LogInformation("Slow-mode override: canceled pending streaming chunk transcriptions");
            }

            _ = Task.Run(async () => await StopAndTranscribeAsync());
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    void IDictationService.CancelTranscription() => _cancellationCoordinator.CancelTranscription();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Dictation worker starting - ScrollLock to record, Pause to cancel");

        try
        {
            // Wire the key handler to the worker's state + action surface.
            // The handler owns the IKeyboardMonitor subscription + the
            // ScrollLock/Pause routing tree; the worker just exposes the
            // four bindings the handler can trigger via IDictationKeyHandlerBindings.
            _keyHandler.Start(new KeyHandlerBindings(this));

            // Broadcaster owns the state-machine + transcriber event
            // subscriptions; TranscriptionCompleted is worker-scoped so the
            // broadcaster gets add/remove hooks for it.
            _stateBroadcaster.Start(
                handler => TranscriptionCompleted += handler,
                handler => TranscriptionCompleted -= handler);

            // Wait for cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Dictation worker stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in dictation worker");
            throw;
        }
        finally
        {
            _keyHandler.Stop();
            _stateBroadcaster.Stop();

            // Stop recording if active
            if (_stateMachine.CurrentState == DictationState.Recording)
            {
                await _cancellationCoordinator.ShutdownAsync();
            }
        }
    }

    /// <summary>
    /// Tiny adapter that exposes the worker's state + the four key-triggered
    /// actions to <see cref="IDictationKeyHandler"/> without leaking the
    /// whole worker into the handler. Keeping it nested avoids polluting the
    /// namespace with a helper type that only the worker's factory call
    /// uses. (#969 god-class split.)
    /// </summary>
    private sealed class KeyHandlerBindings(DictationWorker worker) : IDictationKeyHandlerBindings
    {
        public bool IsEnabled => worker._dictationEnabled;
        public DictationState State => worker._stateMachine.CurrentState;

        public Task StartAsync()
        {
            worker._quickDictationMode = false;
            return worker.StartRecordingAsync();
        }

        public Task StopAndTranscribeAsync() => worker.StopAndTranscribeAsync();

        public Task CancelRecordingAsync() => worker._cancellationCoordinator.CancelRecordingAsync();

        public void CancelTranscription() => worker._cancellationCoordinator.CancelTranscription();
    }

    private async Task StartRecordingAsync()
    {
        try
        {
            // Transition to Recording state
            _stateMachine.TransitionTo(DictationState.Recording);

            // Freeze streaming choice for this session — toggles mid-recording have no effect.
            // The session owns transcriber.BeginSession + chunking toggle + audio start.
            await _recordingSession.StartAsync(_streamingTranscriptionEnabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start recording");
            _stateMachine.TransitionTo(DictationState.Idle);
        }
    }

    /// <summary>
    /// Orchestrates the dictation workflow: stop recording, transcribe, save, and type text.
    /// In quick mode: raw STT only (no LLM), auto-paste + auto-Enter.
    /// </summary>
    private async Task StopAndTranscribeAsync()
    {
        try
        {
            var audioData = await ValidateAndPrepareAudioAsync();
            if (audioData == null) return;

            var token = _cancellationCoordinator.BeginTranscription();

            if (_quickDictationMode)
            {
                await _completionPipeline.CompleteQuickAsync(audioData, token);
            }
            else
            {
                await _completionPipeline.CompleteFullAsync(
                    audioData,
                    text => TranscriptionCompleted?.Invoke(this, text),
                    token);
            }
        }
        catch (OperationCanceledException)
        {
            // Pipeline's finally already ran StopTypingFeedback + Idle transition
            // (on both successful and faulted exit); worker only logs here.
            _logger.LogInformation("Transcription canceled by user");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during transcription");
        }
        finally
        {
            _cancellationCoordinator.EndTranscription();
            _recordingSession.EndSession();
        }
    }

    /// <summary>
    /// Stops recording and validates audio data.
    /// Returns null if audio is invalid, otherwise returns audio data.
    /// </summary>
    private async Task<byte[]?> ValidateAndPrepareAudioAsync()
    {
        var audioData = await _recordingSession.StopAsync();

        if (audioData.Length == 0)
        {
            _logger.LogWarning("No audio data recorded");
            _stateMachine.TransitionTo(DictationState.Idle);
            return null;
        }

        _logger.LogInformation("Recording stopped - {Bytes} bytes captured", audioData.Length);
        _stateMachine.TransitionTo(DictationState.Transcribing);

        return audioData;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Dictation worker stopping...");

        if (_stateMachine.CurrentState == DictationState.Recording)
        {
            await _cancellationCoordinator.ShutdownAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}
