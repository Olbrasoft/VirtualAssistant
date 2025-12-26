using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Events;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Voice.Audio;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Workers;

/// <summary>
/// Worker responsible for Voice Activity Detection and buffer management.
/// Subscribes to audio events, performs VAD, manages state machine.
/// Single Responsibility: VAD and state transitions.
/// </summary>
public class VoiceActivityWorker : BackgroundService
{
    private readonly ILogger<VoiceActivityWorker> _logger;
    private readonly IEventBus _eventBus;
    private readonly IVadService _vad;
    private readonly IVoiceStateMachine _stateMachine;
    private readonly ISpeechBufferManager _bufferManager;
    private readonly int _maxSilenceDurationMs;
    private readonly int _minSpeechDurationMs;
    private readonly IDisposable _audioChunkSubscription;

    public VoiceActivityWorker(
        ILogger<VoiceActivityWorker> logger,
        IEventBus eventBus,
        IVadService vad,
        IVoiceStateMachine stateMachine,
        ISpeechBufferManager bufferManager,
        IOptions<ContinuousListenerOptions> options)
    {
        _logger = logger;
        _eventBus = eventBus;
        _vad = vad;
        _stateMachine = stateMachine;
        _bufferManager = bufferManager;
        _maxSilenceDurationMs = options.Value.PostSilenceMs;
        _minSpeechDurationMs = options.Value.MinRecordingMs;

        // Subscribe to audio chunk events
        _audioChunkSubscription = _eventBus.Subscribe<AudioChunkCapturedEvent>(OnAudioChunkCaptured);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("VoiceActivityWorker started");

        try
        {
            // Keep service alive while listening to events
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("VoiceActivityWorker stopped");
        }
    }

    private async Task OnAudioChunkCaptured(AudioChunkCapturedEvent @event, CancellationToken cancellationToken)
    {
        var (isSpeech, rms) = _vad.Analyze(@event.AudioData);
        var currentState = _stateMachine.CurrentState;

        switch (currentState)
        {
            case VoiceState.Muted:
                // Do nothing when muted
                break;

            case VoiceState.Waiting:
                if (isSpeech)
                {
                    _stateMachine.StartRecording(rms);
                    _bufferManager.TransferPreBufferToSpeech();
                    _bufferManager.AddToSpeechBuffer(@event.AudioData);

                    await _eventBus.PublishAsync(new SpeechDetectedEvent(rms), cancellationToken);
                }
                else
                {
                    _bufferManager.AddToPreBuffer(@event.AudioData);
                }
                break;

            case VoiceState.Recording:
                _bufferManager.AddToSpeechBuffer(@event.AudioData);

                if (isSpeech)
                {
                    // Reset silence timer
                    _stateMachine.SilenceStartTime = default;
                }
                else
                {
                    // Track silence
                    if (_stateMachine.SilenceStartTime == default)
                    {
                        _stateMachine.SilenceStartTime = DateTime.UtcNow;
                    }

                    var silenceDuration = (DateTime.UtcNow - _stateMachine.SilenceStartTime).TotalMilliseconds;

                    if (silenceDuration >= _maxSilenceDurationMs)
                    {
                        // Check minimum speech duration
                        var speechDuration = (DateTime.UtcNow - _stateMachine.RecordingStartTime).TotalMilliseconds;

                        if (speechDuration >= _minSpeechDurationMs)
                        {
                            // Complete recording
                            var audioBuffer = _bufferManager.GetCombinedSpeechData();
                            var durationMs = (int)speechDuration;

                            _stateMachine.ResetToWaiting();
                            _bufferManager.ClearSpeechBuffer();

                            await _eventBus.PublishAsync(new SpeechEndedEvent(audioBuffer, durationMs), cancellationToken);
                        }
                        else
                        {
                            // Too short, reset to waiting
                            _logger.LogDebug("Speech too short ({Duration}ms < {Min}ms), ignoring",
                                speechDuration, _minSpeechDurationMs);
                            _stateMachine.ResetToWaiting();
                            _bufferManager.ClearSpeechBuffer();
                        }
                    }
                }
                break;
        }
    }

    public override void Dispose()
    {
        _audioChunkSubscription?.Dispose();
        base.Dispose();
    }
}
