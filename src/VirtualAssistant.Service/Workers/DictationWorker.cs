using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Audio;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Keyboard;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Voice.StateMachine;

namespace Olbrasoft.VirtualAssistant.Service.Workers;

/// <summary>
/// Background worker for keyboard-triggered dictation workflow.
/// Manages audio recording, transcription, and text insertion based on CapsLock state.
/// Uses dedicated audio capture instance (independent from continuous listening).
/// </summary>
public class DictationWorker : BackgroundService, IDictationControl
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
    private readonly DictationOptions _options;

    private CancellationTokenSource? _transcriptionCts;
    private bool _dictationEnabled = true;

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
            await Task.Delay(_options.KeyboardLedSettleTimeMs);

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
    /// </summary>
    private async Task StopAndTranscribeAsync()
    {
        try
        {
            var audioData = await ValidateAndPrepareAudioAsync();
            if (audioData == null) return;

            var transcriptionResult = await TranscribeAudioWithSoundAsync(audioData);
            if (transcriptionResult == null) return;

            await SaveTranscriptionToDatabaseAsync(audioData, transcriptionResult);

            await TypeTextAndFinishAsync(transcriptionResult.Text);
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
        if (correctedText != null && result.LlmDurationMs.HasValue)
        {
            correctionResult = new LlmCorrectionResult(
                CorrectedText: correctedText,
                PromptId: result.PromptId,  // PromptId from TranscriptionService (context-aware prompt selection)
                DurationMs: result.LlmDurationMs.Value
            );
        }

        await persistenceService.SaveTranscriptionAsync(
            audioData,
            originalText,
            correctionResult,
            _transcriptionCts!.Token);
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
