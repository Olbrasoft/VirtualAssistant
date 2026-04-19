using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Audio;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;
using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Voice.StateMachine;
using Olbrasoft.VirtualAssistant.Service.Hubs;
using Olbrasoft.VirtualAssistant.Service.Workers.Dictation;
using Olbrasoft.VirtualAssistant.Service.Workers.Streaming;

namespace Olbrasoft.VirtualAssistant.Service.Workers;

/// <summary>
/// Background worker for keyboard-triggered dictation workflow.
/// Manages audio recording, transcription, and text insertion based on CapsLock state.
/// Uses dedicated audio capture instance (independent from continuous listening).
/// </summary>
public class DictationWorker : BackgroundService, IDictationControl, IDictationService
{
    private readonly ILogger<DictationWorker> _logger;
    private readonly IKeyboardMonitor _keyboardMonitor;
    private readonly IDictationStateMachine _stateMachine;
    private readonly IAudioRecordingCoordinator _recordingCoordinator;
    private readonly ITranscriptionService _transcriptionService;
    private readonly IDictationOutputChannel _outputChannel;
    private readonly IDictationTranscriptionPersister _persister;
    private readonly IClaudeCodeCivilityTrimmer _civilityTrimmer;
    private readonly IStreamingChunkAssembler _streamingAssembler;
    private readonly DictationOptions _options;

    private CancellationTokenSource? _transcriptionCts;
    private bool _dictationEnabled = true;
    private bool _quickDictationMode;
    private volatile bool _streamingTranscriptionEnabled;

    /// <inheritdoc/>
    public DictationState State => _stateMachine.CurrentState;

    /// <inheritdoc/>
    public event EventHandler<string>? TranscriptionCompleted;

    public DictationWorker(
        ILogger<DictationWorker> logger,
        IKeyboardMonitor keyboardMonitor,
        IDictationStateMachine stateMachine,
        IAudioRecordingCoordinator recordingCoordinator,
        ITranscriptionService transcriptionService,
        IDictationOutputChannel outputChannel,
        IDictationTranscriptionPersister persister,
        IClaudeCodeCivilityTrimmer civilityTrimmer,
        IStreamingChunkAssembler streamingAssembler,
        IOptions<DictationOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _keyboardMonitor = keyboardMonitor ?? throw new ArgumentNullException(nameof(keyboardMonitor));
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _recordingCoordinator = recordingCoordinator ?? throw new ArgumentNullException(nameof(recordingCoordinator));
        _transcriptionService = transcriptionService ?? throw new ArgumentNullException(nameof(transcriptionService));
        _outputChannel = outputChannel ?? throw new ArgumentNullException(nameof(outputChannel));
        _persister = persister ?? throw new ArgumentNullException(nameof(persister));
        _civilityTrimmer = civilityTrimmer ?? throw new ArgumentNullException(nameof(civilityTrimmer));
        _streamingAssembler = streamingAssembler ?? throw new ArgumentNullException(nameof(streamingAssembler));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
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
            Task.Run(async () => await EmergencyStopAsync());
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
            if (!quickMode && _streamingAssembler.IsActive)
            {
                _streamingAssembler.CancelAndClear();
                _recordingCoordinator.DisableChunking();
                _logger.LogInformation("Slow-mode override: canceled pending streaming chunk transcriptions");
            }

            _ = Task.Run(async () => await StopAndTranscribeAsync());
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    void IDictationService.CancelTranscription()
    {
        CancelTranscription();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Dictation worker starting - ScrollLock to record, Pause to cancel");

        try
        {
            // Subscribe to keyboard events
            _keyboardMonitor.KeyReleased += OnKeyReleased;

            // Subscribe to state changes for SignalR broadcasting
            _stateMachine.StateChanged += OnStateChangedBroadcast;
            TranscriptionCompleted += OnTranscriptionCompletedBroadcast;
            _transcriptionService.RawTranscriptionReady += OnRawTranscriptionReadyBroadcast;

            // Subscribe to streaming chunk emissions — transcribes each chunk in background
            _recordingCoordinator.ChunkAvailable += OnChunkAvailable;

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
            _keyboardMonitor.KeyReleased -= OnKeyReleased;
            _stateMachine.StateChanged -= OnStateChangedBroadcast;
            TranscriptionCompleted -= OnTranscriptionCompletedBroadcast;
            _transcriptionService.RawTranscriptionReady -= OnRawTranscriptionReadyBroadcast;
            _recordingCoordinator.ChunkAvailable -= OnChunkAvailable;

            // Stop recording if active
            if (_stateMachine.CurrentState == DictationState.Recording)
            {
                await StopRecordingAsync();
            }
        }
    }

    /// <summary>
    /// Event handler for key release. Uses fire-and-forget pattern
    /// to delegate to async handler with proper exception handling.
    /// </summary>
    private void OnKeyReleased(object? sender, KeyEventArgs e)
    {
        _ = HandleKeyReleasedAsync(e);
    }

    /// <summary>
    /// Handles key release events and manages dictation workflow accordingly.
    /// </summary>
    private async Task HandleKeyReleasedAsync(KeyEventArgs e)
    {
        try
        {
            // Ignore all keys when dictation is disabled
            if (!_dictationEnabled)
                return;

            // Pause - cancel recording or transcription
            if (e.Key == KeyCode.Pause)
            {
                var state = _stateMachine.CurrentState;

                // Cancel during recording - discard audio, play cancel sound
                if (state == DictationState.Recording)
                {
                    _logger.LogInformation("Pause pressed during recording - canceling dictation");
                    await CancelRecordingAsync();
                    return;
                }

                // Cancel during transcription
                if (state == DictationState.Transcribing)
                {
                    _logger.LogInformation("Pause pressed - canceling transcription");
                    CancelTranscription();
                    return;
                }
            }

            // Only handle ScrollLock
            if (e.Key != KeyCode.ScrollLock)
                return;

            var currentState = _stateMachine.CurrentState;
            _logger.LogDebug("ScrollLock released - State: {State}", currentState);

            // Toggle logic: ScrollLock toggles between Idle and Recording
            // Idle → Start recording
            if (currentState == DictationState.Idle)
            {
                _logger.LogInformation("ScrollLock pressed - starting dictation");
                _quickDictationMode = false;
                _ = Task.Run(async () => await StartRecordingAsync());
            }
            // Recording → Stop and transcribe
            else if (currentState == DictationState.Recording)
            {
                _logger.LogInformation("ScrollLock pressed - stopping dictation");
                _ = Task.Run(async () => await StopAndTranscribeAsync());
            }
            // Transcribing → Ignore (use Pause to cancel)
            else if (currentState == DictationState.Transcribing)
            {
                _logger.LogDebug("ScrollLock pressed during transcription - ignored (use Pause to cancel)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling key release");
        }
    }

    private void OnStateChangedBroadcast(object? sender, DictationState state)
    {
        var eventType = state switch
        {
            DictationState.Recording => DictationEventType.RecordingStarted,
            DictationState.Transcribing => DictationEventType.TranscriptionStarted,
            _ => DictationEventType.RecordingStopped
        };

        _ = BroadcastDictationEventAsync(eventType, null);
    }

    private void OnTranscriptionCompletedBroadcast(object? sender, string text)
    {
        _ = BroadcastDictationEventAsync(DictationEventType.TranscriptionCompleted, text);
    }

    private void OnRawTranscriptionReadyBroadcast(string text)
    {
        _ = BroadcastDictationEventAsync(DictationEventType.RawTranscriptionCompleted, text);
    }

    /// <summary>
    /// Forwards a streaming audio chunk to <see cref="IStreamingChunkAssembler"/>
    /// which runs the per-chunk Whisper call on a background task and stores
    /// the result keyed by chunk index. Ignored when streaming is not active
    /// for the current session.
    /// </summary>
    private void OnChunkAvailable(object? sender, AudioChunkEventArgs e)
    {
        _streamingAssembler.SubmitChunk(e.Index, e.PcmBytes);
    }

    private Task BroadcastDictationEventAsync(DictationEventType eventType, string? text) =>
        _outputChannel.BroadcastEventAsync(eventType, text);

    private async Task StartRecordingAsync()
    {
        try
        {
            // Transition to Recording state
            _stateMachine.TransitionTo(DictationState.Recording);

            // Freeze streaming choice for this session — toggles mid-recording have no effect
            var streamingActive = _streamingTranscriptionEnabled;
            _streamingAssembler.Reset(streamingActive);

            if (streamingActive)
            {
                _recordingCoordinator.EnableChunking(TimeSpan.FromSeconds(8));
                _logger.LogInformation("Streaming transcription active for this session (8s chunks)");
            }
            else
            {
                _recordingCoordinator.DisableChunking();
            }

            // Start audio recording via coordinator
            await _recordingCoordinator.StartRecordingAsync();
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

            TranscriptionResult? transcriptionResult;

            if (_quickDictationMode)
            {
                transcriptionResult = await TranscribeRawWithSoundAsync(audioData);
            }
            else
            {
                transcriptionResult = await TranscribeAudioWithSoundAsync(audioData);
            }

            if (transcriptionResult == null) return;

            await SaveTranscriptionToDatabaseAsync(audioData, transcriptionResult);

            if (_quickDictationMode)
            {
                // Strip trailing civility ("… Děkuji.", "… Ahoj.", Whisper
                // hallucinated sign-offs) when pasting into a CLI agent that
                // reads the text as a prompt. Scoped to Claude Code only — in
                // chat apps "Děkuji." is a legitimate message and must stay.
                var textToPaste = await StripCivilityForClaudeCodeAsync(transcriptionResult.Text, _transcriptionCts!.Token);
                if (string.IsNullOrWhiteSpace(textToPaste))
                {
                    _logger.LogInformation("Quick dictation: transcription reduced to empty after civility trim — skipping paste");
                    _outputChannel.StopTypingFeedback();
                    _stateMachine.TransitionTo(DictationState.Idle);
                    return;
                }

                // Quick mode: fast paste without clipboard save/restore
                var pasteSucceeded = await _outputChannel.FastPasteAsync(textToPaste, _transcriptionCts!.Token);
                _outputChannel.StopTypingFeedback();

                if (!pasteSucceeded)
                {
                    _logger.LogWarning("Quick dictation: fast paste failed");
                    _stateMachine.TransitionTo(DictationState.Idle);
                    return;
                }

                // Broadcast QuickTranscriptionCompleted BEFORE idle transition so client sets pending flag first
                await BroadcastDictationEventAsync(DictationEventType.QuickTranscriptionCompleted, textToPaste);
                _stateMachine.TransitionTo(DictationState.Idle);
                _logger.LogInformation("Quick dictation: text pasted, QuickTranscriptionCompleted sent");
            }
            else
            {
                // Raise TranscriptionCompleted before typing so remote UI gets the text immediately
                TranscriptionCompleted?.Invoke(this, transcriptionResult.Text);
                await TypeTextAndFinishAsync(transcriptionResult.Text);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Transcription canceled by user");
            _outputChannel.StopTypingFeedback();
            _stateMachine.TransitionTo(DictationState.Idle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during transcription");
            _outputChannel.StopTypingFeedback();
            _stateMachine.TransitionTo(DictationState.Idle);
        }
        finally
        {
            _transcriptionCts?.Dispose();
            _transcriptionCts = null;
            ResetStreamingSessionState();
        }
    }

    /// <summary>
    /// Clears streaming chunk state after a dictation session completes (any path).
    /// Delegates the concurrent-collection cleanup to <see cref="IStreamingChunkAssembler"/>;
    /// the worker still owns the recording-coordinator's chunking flag.
    /// </summary>
    private void ResetStreamingSessionState()
    {
        _streamingAssembler.CancelAndClear();
        _recordingCoordinator.DisableChunking();
    }

    /// <summary>
    /// Stops recording and validates audio data.
    /// Returns null if audio is invalid, otherwise returns audio data.
    /// </summary>
    private async Task<byte[]?> ValidateAndPrepareAudioAsync()
    {
        var audioData = await _recordingCoordinator.StopRecordingAsync();

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

    /// <summary>
    /// Transcribes audio using raw STT only (no LLM correction) with typing sound effect.
    /// Used for quick dictation mode. When streaming was active for this session,
    /// assembles the cached chunk transcriptions instead of running Whisper on the full buffer.
    /// </summary>
    private async Task<TranscriptionResult?> TranscribeRawWithSoundAsync(byte[] audioData)
    {
        _outputChannel.StartTypingFeedback();
        _transcriptionCts = new CancellationTokenSource();

        TranscriptionResult result;
        if (_streamingAssembler.IsActive)
        {
            var combined = await _streamingAssembler.CombineAsync(_transcriptionCts.Token);
            if (string.IsNullOrWhiteSpace(combined))
            {
                _logger.LogWarning("Streaming quick transcription produced empty text; falling back to full-buffer Whisper");
                result = await _transcriptionService.TranscribeRawAsync(audioData, _transcriptionCts.Token);
            }
            else
            {
                _logger.LogInformation(
                    "Streaming quick transcription: combined {Count} chunks into {Length} chars",
                    _streamingAssembler.CompletedChunkCount, combined.Length);
                result = await _transcriptionService.FinalizePreTranscribedRawAsync(combined, _transcriptionCts.Token);
            }
        }
        else
        {
            result = await _transcriptionService.TranscribeRawAsync(audioData, _transcriptionCts.Token);
        }

        if (!result.Success || string.IsNullOrWhiteSpace(result.Text))
        {
            _logger.LogWarning("Quick transcription failed or empty");
            _outputChannel.StopTypingFeedback();
            _stateMachine.TransitionTo(DictationState.Idle);
            return null;
        }

        _logger.LogInformation("Quick transcription: '{Text}'", result.Text);
        return result;
    }

    /// <summary>
    /// Transcribes audio with typing sound effect.
    /// Returns null if transcription failed, otherwise returns transcription result.
    /// </summary>
    private async Task<TranscriptionResult?> TranscribeAudioWithSoundAsync(byte[] audioData)
    {
        _outputChannel.StartTypingFeedback();
        _transcriptionCts = new CancellationTokenSource();

        var result = await _transcriptionService.TranscribeAsync(audioData, _transcriptionCts.Token);

        if (!result.Success || string.IsNullOrWhiteSpace(result.Text))
        {
            _logger.LogWarning("Transcription failed or empty");
            _outputChannel.StopTypingFeedback();
            _stateMachine.TransitionTo(DictationState.Idle);
            return null;
        }

        _logger.LogInformation("Transcription result: '{Text}'", result.Text);
        return result;
    }

    /// <summary>
    /// Saves transcription to database via <see cref="IDictationTranscriptionPersister"/>,
    /// which owns the scoped <c>IDictationPersistenceService</c> resolution and
    /// the racing/non-racing save dispatch. (#969 extraction.)
    /// </summary>
    private Task SaveTranscriptionToDatabaseAsync(byte[] audioData, TranscriptionResult result) =>
        _persister.SaveAsync(audioData, result, _transcriptionCts!.Token);

    /// <summary>
    /// Thin wrapper around <see cref="IClaudeCodeCivilityTrimmer.TrimIfClaudeCodeAsync"/>
    /// that the quick-dictation path calls before pasting. (#969 extraction
    /// moved the detection + trimming into a dedicated helper.)
    /// </summary>
    private Task<string> StripCivilityForClaudeCodeAsync(string text, CancellationToken cancellationToken) =>
        _civilityTrimmer.TrimIfClaudeCodeAsync(text, cancellationToken);

    /// <summary>
    /// Types text into active window and transitions to Idle state.
    /// </summary>
    private async Task TypeTextAndFinishAsync(string text)
    {
        var typed = await _outputChannel.TypeIntoActiveWindowAsync(text, _transcriptionCts!.Token);

        _outputChannel.StopTypingFeedback();

        if (!typed)
        {
            _logger.LogWarning("Failed to type text into active window");
            _stateMachine.TransitionTo(DictationState.Idle);
            return;
        }

        _logger.LogInformation("Text typed successfully into active window");
        _stateMachine.TransitionTo(DictationState.Idle);
    }

    private async Task CancelRecordingAsync()
    {
        try
        {
            _logger.LogInformation("Canceling recording");

            // Emergency stop recording via coordinator (discards audio data)
            await _recordingCoordinator.EmergencyStopAsync();

            // Play cancel sound (paper-rip effect)
            _outputChannel.PlayCancelCue();

            // Return to Idle without transcription
            _stateMachine.TransitionTo(DictationState.Idle);

            _logger.LogInformation("Recording canceled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error canceling recording");
            _stateMachine.TransitionTo(DictationState.Idle);
        }
    }

    private async Task EmergencyStopAsync()
    {
        try
        {
            // Emergency stop recording via coordinator (discards audio data)
            await _recordingCoordinator.EmergencyStopAsync();

            // Return to Idle without transcription
            _stateMachine.TransitionTo(DictationState.Idle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during emergency stop");
            _stateMachine.TransitionTo(DictationState.Idle);
        }
        finally
        {
            ResetStreamingSessionState();
        }
    }

    private void CancelTranscription()
    {
        try
        {
            var currentState = _stateMachine.CurrentState;
            _logger.LogInformation("CancelTranscription called in state {State}", currentState);

            // Stop typing sound immediately
            _outputChannel.StopTypingFeedback();

            // Play cancel sound (paper-rip effect)
            _outputChannel.PlayCancelCue();

            // If still recording, stop audio capture and discard buffer.
            // Must complete before resetting streaming state so no more
            // chunk events arrive after the reset.
            if (currentState == DictationState.Recording)
            {
                _logger.LogInformation("Canceling active recording (emergency stop)");
                // Documented sync-over-async exception (#976): CancelTranscription
                // implements IDictationService.CancelTranscription() which is a
                // void interface method (synchronous cancel semantics). Making it
                // async would ripple through the hub + state-machine call sites
                // without any runtime benefit — this is a user-initiated,
                // infrequent cancel path, not the transcription/streaming hot path.
                _recordingCoordinator.EmergencyStopAsync().GetAwaiter().GetResult();
            }

            // Cancel transcription if in progress
            _transcriptionCts?.Cancel();
            _transcriptionCts?.Dispose();
            _transcriptionCts = null;

            // Cancel in-flight streaming chunk tasks and clear chunk state,
            // otherwise they keep running into the next session.
            ResetStreamingSessionState();

            // Return to Idle
            _stateMachine.TransitionTo(DictationState.Idle);

            _logger.LogInformation("Dictation canceled from state {State}", currentState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error canceling transcription");
        }
    }

    private async Task StopRecordingAsync()
    {
        _logger.LogInformation("Stopping recording on worker shutdown");

        try
        {
            // Stop recording via coordinator (discards audio data on shutdown)
            await _recordingCoordinator.EmergencyStopAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error stopping recording on shutdown");
        }

        _stateMachine.TransitionTo(DictationState.Idle);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Dictation worker stopping...");

        if (_stateMachine.CurrentState == DictationState.Recording)
        {
            await StopRecordingAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}
