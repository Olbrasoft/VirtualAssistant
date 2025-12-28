using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Audio;
using Olbrasoft.VirtualAssistant.Core.Keyboard;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Voice.Audio;
using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.VirtualAssistant.Voice.StateMachine;
using VirtualAssistant.Data;

namespace Olbrasoft.VirtualAssistant.Service.Workers;

/// <summary>
/// Background worker for keyboard-triggered dictation workflow.
/// Manages audio recording, transcription, and text insertion based on CapsLock state.
/// Uses dedicated audio capture instance (independent from continuous listening).
/// </summary>
public class DictationWorker : BackgroundService
{
    private readonly ILogger<DictationWorker> _logger;
    private readonly IKeyboardMonitor _keyboardMonitor;
    private readonly IDictationStateMachine _stateMachine;
    private readonly IAudioCaptureService _audioCapture;
    private readonly ITranscriptionService _transcriptionService;
    private readonly IKeyboardSimulationService _keyboardSimulation;
    private readonly TypingSoundPlayer _typingSound;
    private readonly IServiceScopeFactory _scopeFactory;

    private CancellationTokenSource? _recordingCts;
    private CancellationTokenSource? _transcriptionCts;
    private List<byte> _audioBuffer = new();
    private readonly object _bufferLock = new();
    private Task? _recordingTask;
    private bool _dictationEnabled = false;

    public DictationWorker(
        ILogger<DictationWorker> logger,
        IKeyboardMonitor keyboardMonitor,
        IDictationStateMachine stateMachine,
        IAudioCaptureService audioCapture,
        ITranscriptionService transcriptionService,
        IKeyboardSimulationService keyboardSimulation,
        TypingSoundPlayer typingSound,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _keyboardMonitor = keyboardMonitor ?? throw new ArgumentNullException(nameof(keyboardMonitor));
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _audioCapture = audioCapture ?? throw new ArgumentNullException(nameof(audioCapture));
        _transcriptionService = transcriptionService ?? throw new ArgumentNullException(nameof(transcriptionService));
        _keyboardSimulation = keyboardSimulation ?? throw new ArgumentNullException(nameof(keyboardSimulation));
        _typingSound = typingSound ?? throw new ArgumentNullException(nameof(typingSound));
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
        _logger.LogInformation("Dictation worker starting - CapsLock to record, Escape to cancel");

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

    private void OnKeyReleased(object? sender, KeyEventArgs e)
    {
        try
        {
            // Ignore all keys when dictation is disabled
            if (!_dictationEnabled)
                return;

            // Escape - cancel transcription
            if (e.Key == KeyCode.Escape && _stateMachine.CurrentState == DictationState.Transcribing)
            {
                _logger.LogInformation("Escape pressed - canceling transcription");
                CancelTranscription();
                return;
            }

            // Only handle CapsLock
            if (e.Key != KeyCode.CapsLock)
                return;

            // Small delay to ensure LED state is updated by kernel
            Thread.Sleep(50);

            var capsLockOn = _keyboardMonitor.IsCapsLockOn();
            var currentState = _stateMachine.CurrentState;

            _logger.LogDebug("CapsLock released - LED: {CapsLockOn}, State: {State}", capsLockOn, currentState);

            // CapsLock ON + Idle → Start recording
            if (capsLockOn && currentState == DictationState.Idle)
            {
                _logger.LogInformation("CapsLock ON - starting dictation");
                Task.Run(async () => await StartRecordingAsync());
            }
            // CapsLock OFF + Recording → Stop and transcribe
            else if (!capsLockOn && currentState == DictationState.Recording)
            {
                _logger.LogInformation("CapsLock OFF - stopping dictation");
                Task.Run(async () => await StopAndTranscribeAsync());
            }
            // CapsLock ON + Recording → Emergency cancel
            else if (capsLockOn && currentState == DictationState.Recording)
            {
                _logger.LogWarning("CapsLock toggled during recording - emergency stop");
                Task.Run(async () => await EmergencyStopAsync());
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

            // Stop typing sound after transcription (before typing text)
            _typingSound.StopLoop();

            // Save transcription to database
            int? whisperTranscriptionId = null;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var whisperRepo = scope.ServiceProvider.GetRequiredService<IWhisperTranscriptionRepository>();

                // Calculate audio duration from audio data (16-bit mono @ 16kHz)
                // duration_ms = (bytes / 2 bytes_per_sample) / 16000 samples_per_second * 1000 ms_per_second
                var audioDurationMs = (int)((audioData.Length / 2.0) / 16000.0 * 1000.0);

                // Save original Whisper transcription (before LLM correction)
                var originalText = result.OriginalText ?? result.Text;
                var transcription = await whisperRepo.SaveAsync(
                    originalText,
                    durationMs: audioDurationMs,
                    _transcriptionCts.Token);

                whisperTranscriptionId = transcription.Id;
                _logger.LogDebug("Saved Whisper transcription to database with ID {Id}", whisperTranscriptionId);

                // If LLM correction was applied, save it to database
                if (result.OriginalText != null && result.Text != result.OriginalText)
                {
                    var llmRepo = scope.ServiceProvider.GetRequiredService<ILlmCorrectionRepository>();

                    var correction = await llmRepo.SaveAsync(
                        whisperTranscriptionId: transcription.Id,
                        correctedText: result.Text,
                        durationMs: 0, // TODO: Track LLM call duration
                        _transcriptionCts.Token);

                    _logger.LogDebug("Saved LLM correction {Id} for transcription {TranscriptionId}: '{Original}' → '{Corrected}'",
                        correction.Id, transcription.Id,
                        originalText.Length > 30 ? originalText[..30] + "..." : originalText,
                        result.Text.Length > 30 ? result.Text[..30] + "..." : result.Text);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save Whisper transcription to database");
                // Continue with dictation even if save failed
            }

            // Type text into active window via xdotool
            var typed = await _keyboardSimulation.TypeIntoActiveWindowAsync(result.Text, _transcriptionCts.Token);

            if (!typed)
            {
                _logger.LogWarning("Failed to type text into active window");
                _typingSound.StopLoop();
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
