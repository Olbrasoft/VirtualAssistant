using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Audio;
using Olbrasoft.VirtualAssistant.Core.Keyboard;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Voice.Audio;
using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.VirtualAssistant.Voice.StateMachine;

namespace Olbrasoft.VirtualAssistant.Service.Workers;

/// <summary>
/// Background worker for keyboard-triggered dictation workflow.
/// Manages audio recording, transcription, and text insertion based on CapsLock state.
/// Uses dedicated audio capture instance (independent from continuous listening).
/// </summary>
public class DictationWorker : BackgroundService
{
    /// <summary>
    /// Delay in milliseconds to allow keyboard LED state to settle after key release.
    /// Required for reliable CapsLock state detection.
    /// </summary>
    private const int KEYBOARD_LED_SETTLE_TIME_MS = 50;

    private readonly ILogger<DictationWorker> _logger;
    private readonly IKeyboardMonitor _keyboardMonitor;
    private readonly IDictationStateMachine _stateMachine;
    private readonly IAudioCaptureService _audioCapture;
    private readonly ITranscriptionService _transcriptionService;
    private readonly IKeyboardSimulationService _keyboardSimulation;
    private readonly TypingSoundPlayer _typingSound;
    private readonly CancelSoundPlayer _cancelSound;
    private readonly IServiceScopeFactory _scopeFactory;

    private CancellationTokenSource? _recordingCts;
    private CancellationTokenSource? _transcriptionCts;
    private List<byte> _audioBuffer = new();
    private readonly object _bufferLock = new();
    private Task? _recordingTask;
    private bool _dictationEnabled = true;

    public DictationWorker(
        ILogger<DictationWorker> logger,
        IKeyboardMonitor keyboardMonitor,
        IDictationStateMachine stateMachine,
        IAudioCaptureService audioCapture,
        ITranscriptionService transcriptionService,
        IKeyboardSimulationService keyboardSimulation,
        TypingSoundPlayer typingSound,
        CancelSoundPlayer cancelSound,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _keyboardMonitor = keyboardMonitor ?? throw new ArgumentNullException(nameof(keyboardMonitor));
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _audioCapture = audioCapture ?? throw new ArgumentNullException(nameof(audioCapture));
        _transcriptionService = transcriptionService ?? throw new ArgumentNullException(nameof(transcriptionService));
        _keyboardSimulation = keyboardSimulation ?? throw new ArgumentNullException(nameof(keyboardSimulation));
        _typingSound = typingSound ?? throw new ArgumentNullException(nameof(typingSound));
        _cancelSound = cancelSound ?? throw new ArgumentNullException(nameof(cancelSound));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Dictation worker starting - CapsLock to record, Pause to cancel");

        try
        {
            // Subscribe to keyboard events
            _keyboardMonitor.KeyReleased += OnKeyReleased;

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

            // Stop recording if active
            if (_stateMachine.CurrentState == DictationState.Recording)
            {
                await StopRecordingAsync();
            }
        }
    }

    private async void OnKeyReleased(object? sender, KeyEventArgs e)
    {
        try
        {
            // Ignore all keys when dictation is disabled
            if (!_dictationEnabled)
                return;

            // Pause - cancel transcription
            if (e.Key == KeyCode.Pause && _stateMachine.CurrentState == DictationState.Transcribing)
            {
                _logger.LogInformation("Pause pressed - canceling transcription");
                CancelTranscription();
                return;
            }

            // Only handle CapsLock
            if (e.Key != KeyCode.CapsLock)
                return;

            // Small delay to ensure LED state is updated by kernel
            await Task.Delay(KEYBOARD_LED_SETTLE_TIME_MS);

            var capsLockOn = _keyboardMonitor.IsCapsLockOn();
            var currentState = _stateMachine.CurrentState;

            _logger.LogDebug("CapsLock released - LED: {CapsLockOn}, State: {State}", capsLockOn, currentState);

            // CapsLock ON + Idle → Start recording
            if (capsLockOn && currentState == DictationState.Idle)
            {
                _logger.LogInformation("CapsLock ON - starting dictation");
                _ = Task.Run(async () => await StartRecordingAsync());
            }
            // CapsLock OFF + Recording → Stop and transcribe
            else if (!capsLockOn && currentState == DictationState.Recording)
            {
                _logger.LogInformation("CapsLock OFF - stopping dictation");
                _ = Task.Run(async () => await StopAndTranscribeAsync());
            }
            // CapsLock ON + Recording → Emergency cancel
            else if (capsLockOn && currentState == DictationState.Recording)
            {
                _logger.LogWarning("CapsLock toggled during recording - emergency stop");
                _ = Task.Run(async () => await EmergencyStopAsync());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling key release");
        }
    }

    private async Task StartRecordingAsync()
    {
        try
        {
            // Transition to Recording state
            _stateMachine.TransitionTo(DictationState.Recording);

            // Clear audio buffer
            lock (_bufferLock)
            {
                _audioBuffer.Clear();
            }

            // Create cancellation token for recording
            _recordingCts = new CancellationTokenSource();

            // Start audio capture
            _audioCapture.Start();

            // Start recording task to capture audio chunks
            _recordingTask = Task.Run(async () => await CaptureAudioAsync(_recordingCts.Token), _recordingCts.Token);

            _logger.LogInformation("Recording started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start recording");
            _stateMachine.TransitionTo(DictationState.Idle);
        }
    }

    /// <summary>
    /// Captures audio chunks from the audio capture service and buffers them.
    /// </summary>
    private async Task CaptureAudioAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _stateMachine.CurrentState == DictationState.Recording)
            {
                var chunk = await _audioCapture.ReadChunkAsync(cancellationToken);
                if (chunk != null)
                {
                    lock (_bufferLock)
                    {
                        _audioBuffer.AddRange(chunk);
                    }

                    _logger.LogDebug("Audio chunk captured: {Bytes} bytes (total: {Total})", chunk.Length, _audioBuffer.Count);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Audio capture canceled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing audio");
        }
    }

    private async Task StopAndTranscribeAsync()
    {
        try
        {
            // Cancel recording task
            _recordingCts?.Cancel();

            // Wait for recording task to complete
            if (_recordingTask != null)
            {
                try
                {
                    await _recordingTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected when canceling
                }
            }

            // Stop audio capture
            _audioCapture.Stop();

            // Cleanup
            _recordingCts?.Dispose();
            _recordingCts = null;
            _recordingTask = null;

            // Get recorded audio
            byte[] audioData;
            lock (_bufferLock)
            {
                audioData = _audioBuffer.ToArray();
                _audioBuffer.Clear();
            }

            if (audioData.Length == 0)
            {
                _logger.LogWarning("No audio data recorded");
                _stateMachine.TransitionTo(DictationState.Idle);
                return;
            }

            _logger.LogInformation("Recording stopped - {Bytes} bytes captured", audioData.Length);

            // Transition to Transcribing state
            _stateMachine.TransitionTo(DictationState.Transcribing);

            // Start typing sound loop
            _typingSound.StartLoop();

            // Create transcription CTS
            _transcriptionCts = new CancellationTokenSource();

            // Transcribe audio
            var result = await _transcriptionService.TranscribeAsync(audioData, _transcriptionCts.Token);

            if (!result.Success || string.IsNullOrWhiteSpace(result.Text))
            {
                _logger.LogWarning("Transcription failed or empty");
                _typingSound.StopLoop();
                _stateMachine.TransitionTo(DictationState.Idle);
                return;
            }

            _logger.LogInformation("Transcription result: '{Text}'", result.Text);

            // Keep typing sound playing during database save and text insertion
            // It will be stopped after text is typed successfully

            // Save transcription to database using persistence service
            // Note: Create scope because DictationWorker is singleton but persistence service is scoped
            using (var scope = _scopeFactory.CreateScope())
            {
                var persistenceService = scope.ServiceProvider.GetRequiredService<IDictationPersistenceService>();
                var originalText = result.OriginalText ?? result.Text;
                var correctedText = (result.OriginalText != null && result.Text != result.OriginalText) ? result.Text : null;

                await persistenceService.SaveTranscriptionAsync(
                    audioData,
                    originalText,
                    correctedText,
                    result.LlmDurationMs ?? 0,
                    _transcriptionCts.Token);
            }

            // Type text into active window via xdotool
            var typed = await _keyboardSimulation.TypeIntoActiveWindowAsync(result.Text, _transcriptionCts.Token);

            // Stop typing sound after text is inserted (or failed)
            _typingSound.StopLoop();

            if (!typed)
            {
                _logger.LogWarning("Failed to type text into active window");
                _stateMachine.TransitionTo(DictationState.Idle);
                return;
            }

            _logger.LogInformation("Text typed successfully into active window");

            // Return to Idle state
            _stateMachine.TransitionTo(DictationState.Idle);
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
        }
    }

    private async Task EmergencyStopAsync()
    {
        try
        {
            _logger.LogWarning("Emergency stop triggered");

            // Cancel recording task
            _recordingCts?.Cancel();

            // Wait for recording task to complete
            if (_recordingTask != null)
            {
                try
                {
                    await _recordingTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }

            // Stop audio capture
            _audioCapture.Stop();

            // Cleanup
            _recordingCts?.Dispose();
            _recordingCts = null;
            _recordingTask = null;

            // Clear buffer
            lock (_bufferLock)
            {
                _audioBuffer.Clear();
            }

            // Return to Idle without transcription
            _stateMachine.TransitionTo(DictationState.Idle);

            _logger.LogInformation("Emergency stop completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during emergency stop");
            _stateMachine.TransitionTo(DictationState.Idle);
        }
    }

    private void CancelTranscription()
    {
        try
        {
            _logger.LogInformation("Canceling transcription");

            // Stop typing sound immediately
            _typingSound.StopLoop();

            // Play cancel sound (paper-rip effect)
            _cancelSound.Play();

            // Cancel transcription
            _transcriptionCts?.Cancel();
            _transcriptionCts?.Dispose();
            _transcriptionCts = null;

            // Return to Idle
            _stateMachine.TransitionTo(DictationState.Idle);

            _logger.LogInformation("Transcription canceled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error canceling transcription");
        }
    }

    private async Task StopRecordingAsync()
    {
        _logger.LogInformation("Stopping recording on worker shutdown");

        // Cancel recording task
        _recordingCts?.Cancel();

        // Wait for recording task to complete
        if (_recordingTask != null)
        {
            try
            {
                await _recordingTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        // Stop audio capture
        try
        {
            _audioCapture.Stop();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error stopping audio capture on shutdown");
        }

        // Cleanup
        _recordingCts?.Dispose();
        _recordingCts = null;
        _recordingTask = null;

        lock (_bufferLock)
        {
            _audioBuffer.Clear();
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
