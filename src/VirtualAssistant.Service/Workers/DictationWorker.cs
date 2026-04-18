using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Audio;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Keyboard;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;
using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Voice.StateMachine;
using Olbrasoft.VirtualAssistant.Service.Hubs;

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
    private readonly IKeyboardSimulationService _keyboardSimulation;
    private readonly ISoundEffectPlayer _typingSound;
    private readonly ISoundEffectPlayer _cancelSound;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<DictationHub> _hubContext;
    private readonly ICliAppDetector _cliAppDetector;
    private readonly DictationOptions _options;

    private CancellationTokenSource? _transcriptionCts;
    private bool _dictationEnabled = true;
    private bool _quickDictationMode;
    private volatile bool _streamingTranscriptionEnabled;

    // Streaming chunk transcription state — populated during Recording when streaming is on.
    // On Stop (quick mode): assembled into the final raw text. On Stop (slow mode): discarded.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, string> _streamChunkResults = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, Task> _streamChunkTasks = new();
    private CancellationTokenSource? _streamChunksCts;
    private bool _streamingActiveForSession;  // frozen snapshot at StartRecording time

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
        IKeyboardSimulationService keyboardSimulation,
        ISoundEffectPlayer typingSound,
        ISoundEffectPlayer cancelSound,
        IServiceScopeFactory scopeFactory,
        IHubContext<DictationHub> hubContext,
        ICliAppDetector cliAppDetector,
        IOptions<DictationOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _keyboardMonitor = keyboardMonitor ?? throw new ArgumentNullException(nameof(keyboardMonitor));
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _recordingCoordinator = recordingCoordinator ?? throw new ArgumentNullException(nameof(recordingCoordinator));
        _transcriptionService = transcriptionService ?? throw new ArgumentNullException(nameof(transcriptionService));
        _keyboardSimulation = keyboardSimulation ?? throw new ArgumentNullException(nameof(keyboardSimulation));
        _typingSound = typingSound ?? throw new ArgumentNullException(nameof(typingSound));
        _cancelSound = cancelSound ?? throw new ArgumentNullException(nameof(cancelSound));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _cliAppDetector = cliAppDetector ?? throw new ArgumentNullException(nameof(cliAppDetector));
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
            if (!quickMode && _streamingActiveForSession)
            {
                try { _streamChunksCts?.Cancel(); } catch { /* best effort */ }
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
    /// Handles a streaming audio chunk by launching a background Whisper call.
    /// Results are keyed by chunk index in <see cref="_streamChunkResults"/> and assembled
    /// in order on Stop when quick-mode streaming finalizes. Ignored if streaming is not
    /// active for this session or if the chunk arrived outside Recording state.
    /// </summary>
    private void OnChunkAvailable(object? sender, AudioChunkEventArgs e)
    {
        if (!_streamingActiveForSession) return;
        if (_streamChunksCts == null || _streamChunksCts.IsCancellationRequested) return;

        var ct = _streamChunksCts.Token;
        var task = Task.Run(async () =>
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var text = await _transcriptionService.TranscribeChunkRawAsync(e.PcmBytes, ct);
                sw.Stop();
                _streamChunkResults[e.Index] = text ?? string.Empty;
                _logger.LogInformation(
                    "Streaming chunk {Index} transcribed in {ElapsedMs}ms: '{Preview}'",
                    e.Index,
                    sw.ElapsedMilliseconds,
                    (text ?? string.Empty).Length > 60 ? (text ?? string.Empty)[..60] + "…" : text);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Streaming chunk {Index} canceled", e.Index);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Streaming chunk {Index} transcription failed", e.Index);
                _streamChunkResults[e.Index] = string.Empty;
            }
        }, ct);

        _streamChunkTasks[e.Index] = task;
    }

    /// <summary>
    /// Awaits pending chunk transcription tasks and concatenates their ordered results.
    /// Returns null if no chunks were produced (streaming not active or no audio).
    /// </summary>
    private async Task<string?> CombineStreamingChunksAsync(CancellationToken cancellationToken)
    {
        if (!_streamingActiveForSession) return null;

        // Wait for all background chunk transcriptions to complete.
        var pending = _streamChunkTasks.Values.ToArray();
        if (pending.Length == 0)
        {
            _logger.LogInformation("CombineStreamingChunks: no chunks produced");
            return null;
        }

        try
        {
            await Task.WhenAll(pending).WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "One or more streaming chunk tasks faulted; proceeding with available results");
        }

        var ordered = _streamChunkResults
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => kvp.Value?.Trim() ?? string.Empty)
            .Where(t => t.Length > 0);

        var combined = string.Join(" ", ordered);
        return System.Text.RegularExpressions.Regex.Replace(combined, @"\s+", " ").Trim();
    }

    private async Task BroadcastDictationEventAsync(DictationEventType eventType, string? text)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("DictationEvent", new DictationEvent
            {
                EventType = eventType,
                Text = text
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to broadcast dictation event to SignalR clients");
        }
    }

    private async Task StartRecordingAsync()
    {
        try
        {
            // Transition to Recording state
            _stateMachine.TransitionTo(DictationState.Recording);

            // Freeze streaming choice for this session — toggles mid-recording have no effect
            _streamingActiveForSession = _streamingTranscriptionEnabled;
            _streamChunkResults.Clear();
            _streamChunkTasks.Clear();
            _streamChunksCts?.Dispose();
            _streamChunksCts = null;

            if (_streamingActiveForSession)
            {
                _streamChunksCts = new CancellationTokenSource();
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
                    _typingSound.StopLoop();
                    _stateMachine.TransitionTo(DictationState.Idle);
                    return;
                }

                // Quick mode: fast paste without clipboard save/restore
                var pasteSucceeded = await _keyboardSimulation.FastPasteAsync(textToPaste, _transcriptionCts!.Token);
                _typingSound.StopLoop();

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
            _typingSound.StopLoop();
            _stateMachine.TransitionTo(DictationState.Idle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during transcription");
            _typingSound.StopLoop();
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
    /// </summary>
    private void ResetStreamingSessionState()
    {
        try
        {
            _streamChunksCts?.Cancel();
            _streamChunksCts?.Dispose();
        }
        catch { /* best effort */ }
        _streamChunksCts = null;
        _streamChunkResults.Clear();
        _streamChunkTasks.Clear();
        _streamingActiveForSession = false;
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
        _typingSound.StartLoop();
        _transcriptionCts = new CancellationTokenSource();

        TranscriptionResult result;
        if (_streamingActiveForSession)
        {
            var combined = await CombineStreamingChunksAsync(_transcriptionCts.Token);
            if (string.IsNullOrWhiteSpace(combined))
            {
                _logger.LogWarning("Streaming quick transcription produced empty text; falling back to full-buffer Whisper");
                result = await _transcriptionService.TranscribeRawAsync(audioData, _transcriptionCts.Token);
            }
            else
            {
                _logger.LogInformation(
                    "Streaming quick transcription: combined {Count} chunks into {Length} chars",
                    _streamChunkResults.Count, combined.Length);
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
            _typingSound.StopLoop();
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
        _typingSound.StartLoop();
        _transcriptionCts = new CancellationTokenSource();

        var result = await _transcriptionService.TranscribeAsync(audioData, _transcriptionCts.Token);

        if (!result.Success || string.IsNullOrWhiteSpace(result.Text))
        {
            _logger.LogWarning("Transcription failed or empty");
            _typingSound.StopLoop();
            _stateMachine.TransitionTo(DictationState.Idle);
            return null;
        }

        _logger.LogInformation("Transcription result: '{Text}'", result.Text);
        return result;
    }

    /// <summary>
    /// Saves transcription to database using scoped persistence service.
    /// Note: Creates scope because DictationWorker is singleton but persistence service is scoped.
    /// </summary>
    private async Task SaveTranscriptionToDatabaseAsync(byte[] audioData, TranscriptionResult result)
    {
        using var scope = _scopeFactory.CreateScope();
        var persistenceService = scope.ServiceProvider.GetRequiredService<IDictationPersistenceService>();

        var originalText = result.OriginalText ?? result.Text;
        var correctedText = (result.OriginalText != null && result.Text != result.OriginalText) ? result.Text : null;

        // Construct LlmCorrectionResult if LLM correction was applied
        LlmCorrectionResult? correctionResult = null;
        if (correctedText != null && result.ModelId.HasValue && result.LlmDurationMs.GetValueOrDefault() > 0)
        {
            correctionResult = new LlmCorrectionResult(
                CorrectedText: correctedText,
                PromptId: result.PromptId,  // PromptId from TranscriptionService (context-aware prompt selection)
                DurationMs: result.LlmDurationMs.Value,
                ModelId: result.ModelId,  // ModelId from LLM provider
                InputTokens: result.InputTokens,
                OutputTokens: result.OutputTokens,
                ReasoningTokens: result.ReasoningTokens
            );
        }

        // Get the STT provider ID from the transcription result (set by the active speech transcriber)
        // Falls back to Whisper (13) if not set - required because provider_id has FK constraint
        var sttProviderId = result.SttProviderId.GetValueOrDefault(13);

        // Use racing-aware save if race data is present
        if (result.RaceGroupId.HasValue)
        {
            await persistenceService.SaveTranscriptionWithRacingAsync(
                audioData,
                originalText,
                correctionResult,
                sttProviderId,
                result.RaceGroupId.Value,
                result.RacingLoserTask,
                _transcriptionCts!.Token);
        }
        else
        {
            await persistenceService.SaveTranscriptionAsync(
                audioData,
                originalText,
                correctionResult,
                sttProviderId,
                _transcriptionCts!.Token);
        }
    }

    /// <summary>
    /// Applies <see cref="CivilityTrimmer"/> to the dictated text if the active
    /// CLI app is Claude Code. For any other CLI agent or a plain GUI focus,
    /// returns the text unchanged — the trimmer is Claude-Code-scoped per the
    /// feature request ("Děkuji." is a legitimate message in chat apps).
    /// Detection errors fall back to leaving the text untouched — the worst
    /// case here is one stray civility word, worth less than mangling valid
    /// input when gdbus hiccups.
    /// </summary>
    private async Task<string> StripCivilityForClaudeCodeAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        CliAppDetectionResult? cliApp;
        try
        {
            cliApp = await _cliAppDetector.DetectCliAppAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "CLI app detection failed before quick paste — skipping civility trim");
            return text;
        }

        if (cliApp is null || !string.Equals(cliApp.AppName, "Claude Code", StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        var stripped = CivilityTrimmer.StripTrailingCivility(text);
        if (stripped.Length != text.Length)
        {
            _logger.LogInformation(
                "Quick dictation: stripped trailing civility for Claude Code ({Before} → {After} chars)",
                text.Length, stripped.Length);
        }
        return stripped;
    }

    /// <summary>
    /// Types text into active window and transitions to Idle state.
    /// </summary>
    private async Task TypeTextAndFinishAsync(string text)
    {
        var typed = await _keyboardSimulation.TypeIntoActiveWindowAsync(text, _transcriptionCts!.Token);

        _typingSound.StopLoop();

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
            _cancelSound.Play();

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
            _typingSound.StopLoop();

            // Play cancel sound (paper-rip effect)
            _cancelSound.Play();

            // If still recording, stop audio capture and discard buffer.
            // Must complete before resetting streaming state so no more
            // chunk events arrive after the reset.
            if (currentState == DictationState.Recording)
            {
                _logger.LogInformation("Canceling active recording (emergency stop)");
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
