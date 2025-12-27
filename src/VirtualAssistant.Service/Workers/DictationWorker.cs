using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Events;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Core.TextInput;
using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.VirtualAssistant.Voice.StateMachine;

namespace Olbrasoft.VirtualAssistant.Service.Workers;

/// <summary>
/// Background worker for keyboard-triggered dictation workflow.
/// Manages audio recording, transcription, and text insertion based on CapsLock state.
/// </summary>
public class DictationWorker : BackgroundService
{
    private readonly ILogger<DictationWorker> _logger;
    private readonly IKeyboardMonitor _keyboardMonitor;
    private readonly IDictationStateMachine _stateMachine;
    private readonly IEventBus _eventBus;
    private readonly ITranscriptionService _transcriptionService;
    private readonly ITextInputService _textInputService;

    private CancellationTokenSource? _recordingCts;
    private CancellationTokenSource? _transcriptionCts;
    private List<byte> _audioBuffer = new();
    private readonly object _bufferLock = new();
    private IDisposable? _audioEventSubscription;

    public DictationWorker(
        ILogger<DictationWorker> logger,
        IKeyboardMonitor keyboardMonitor,
        IDictationStateMachine stateMachine,
        IEventBus eventBus,
        ITranscriptionService transcriptionService,
        ITextInputService textInputService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _keyboardMonitor = keyboardMonitor ?? throw new ArgumentNullException(nameof(keyboardMonitor));
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _transcriptionService = transcriptionService ?? throw new ArgumentNullException(nameof(transcriptionService));
        _textInputService = textInputService ?? throw new ArgumentNullException(nameof(textInputService));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Dictation worker starting - CapsLock to record, Escape to cancel");

        try
        {
            // Subscribe to keyboard events
            _keyboardMonitor.KeyReleased += OnKeyReleased;

            // Subscribe to audio chunk events via EventBus
            _audioEventSubscription = _eventBus.Subscribe<AudioChunkCapturedEvent>(OnAudioChunkCaptured);

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
            _audioEventSubscription?.Dispose();

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

    private Task OnAudioChunkCaptured(AudioChunkCapturedEvent evt, CancellationToken cancellationToken)
    {
        if (_stateMachine.CurrentState != DictationState.Recording)
            return Task.CompletedTask;

        lock (_bufferLock)
        {
            _audioBuffer.AddRange(evt.AudioData);
        }

        _logger.LogDebug("Audio data buffered: {Bytes} bytes (total: {Total})",
            evt.AudioData.Length, _audioBuffer.Count);

        return Task.CompletedTask;
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

            _logger.LogInformation("Recording started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start recording");
            _stateMachine.TransitionTo(DictationState.Idle);
        }
    }

    private async Task StopAndTranscribeAsync()
    {
        try
        {
            // Cancel recording
            _recordingCts?.Cancel();
            _recordingCts?.Dispose();
            _recordingCts = null;

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

            // Create transcription CTS
            _transcriptionCts = new CancellationTokenSource();

            // Transcribe audio
            var result = await _transcriptionService.TranscribeAsync(audioData, _transcriptionCts.Token);

            if (!result.Success || string.IsNullOrWhiteSpace(result.Text))
            {
                _logger.LogWarning("Transcription failed or empty");
                _stateMachine.TransitionTo(DictationState.Idle);
                return;
            }

            _logger.LogInformation("Transcription result: '{Text}'", result.Text);

            // Type text via OpenCode
            await _textInputService.TypeTextAsync(result.Text, submitPrompt: false);

            _logger.LogInformation("Text typed successfully");

            // Return to Idle state
            _stateMachine.TransitionTo(DictationState.Idle);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Transcription canceled by user");
            _stateMachine.TransitionTo(DictationState.Idle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during transcription");
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

            // Cancel recording
            _recordingCts?.Cancel();
            _recordingCts?.Dispose();
            _recordingCts = null;

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

        _recordingCts?.Cancel();
        _recordingCts?.Dispose();
        _recordingCts = null;

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
