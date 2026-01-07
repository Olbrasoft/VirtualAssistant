using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Voice.StateMachine;
using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Infrastructure;

/// <summary>
/// Coordinates TTS speech with dictation state to prevent audio interference.
/// Ensures TTS is paused during dictation and resumes after completion.
/// Uses segregated interfaces (ISP) - only depends on the specific functionality needed.
/// </summary>
public class DictationSpeechCoordinator : IHostedService, IDisposable
{
    private readonly ILogger<DictationSpeechCoordinator> _logger;
    private readonly IDictationStateMachine _stateMachine;
    private readonly ISpeechLockService _lockService;
    private readonly ISpeechPlaybackState _playbackState;
    private readonly ISpeechQueueInfo _queueInfo;
    private readonly ISpeechCancellation _speechCancellation;
    private bool _disposed;

    public DictationSpeechCoordinator(
        ILogger<DictationSpeechCoordinator> logger,
        IDictationStateMachine stateMachine,
        ISpeechLockService lockService,
        ISpeechPlaybackState playbackState,
        ISpeechQueueInfo queueInfo,
        ISpeechCancellation speechCancellation)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _lockService = lockService ?? throw new ArgumentNullException(nameof(lockService));
        _playbackState = playbackState ?? throw new ArgumentNullException(nameof(playbackState));
        _queueInfo = queueInfo ?? throw new ArgumentNullException(nameof(queueInfo));
        _speechCancellation = speechCancellation ?? throw new ArgumentNullException(nameof(speechCancellation));
    }

    /// <summary>
    /// Starts the coordinator and subscribes to dictation state changes.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DictationSpeechCoordinator starting - subscribing to dictation state changes");

        // Subscribe to dictation state changes
        _stateMachine.StateChanged += OnDictationStateChanged;

        _logger.LogInformation("DictationSpeechCoordinator started successfully");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Event handler for dictation state changes. Uses fire-and-forget pattern
    /// to delegate to async handler with proper exception handling.
    /// </summary>
    private void OnDictationStateChanged(object? sender, DictationState newState)
    {
        _ = HandleDictationStateChangedAsync(newState);
    }

    /// <summary>
    /// Handles dictation state changes and coordinates TTS accordingly.
    /// </summary>
    private async Task HandleDictationStateChangedAsync(DictationState newState)
    {
        try
        {
            _logger.LogInformation("Dictation state changed to: {State}", newState);

            switch (newState)
            {
                case DictationState.Recording:
                case DictationState.Transcribing:
                    // User is dictating - prevent TTS interference
                    HandleDictationActive();
                    break;

                case DictationState.Idle:
                    // Dictation completed - resume TTS
                    await HandleDictationCompleteAsync();
                    break;

                default:
                    _logger.LogWarning("Unknown dictation state: {State}", newState);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling dictation state change to {State}", newState);
        }
    }

    /// <summary>
    /// Handles active dictation state (Recording or Transcribing).
    /// Locks TTS and cancels any currently playing speech.
    /// Only cancels if audio is actually playing (user can hear it).
    /// During generation phase, lock will naturally queue the message.
    /// </summary>
    private void HandleDictationActive()
    {
        _logger.LogInformation("Dictation active - locking TTS");

        // Lock TTS to prevent new playback (no timeout - explicit unlock on completion)
        _lockService.Lock(timeout: null);

        // Cancel ONLY if audio is actually playing (user can hear it)
        // During generation phase, IsPlaying=false, so message gets queued naturally
        if (_playbackState.IsPlaying)
        {
            _logger.LogInformation("Canceling current TTS playback (queue count: {QueueCount})", _queueInfo.QueueCount);
            _speechCancellation.CancelCurrentSpeech();
        }
        else if (_playbackState.IsSpeaking)
        {
            _logger.LogInformation("TTS is generating audio (not playing yet) - will queue naturally (queue count: {QueueCount})", _queueInfo.QueueCount);
        }
    }

    /// <summary>
    /// Handles dictation completion (transition to Idle).
    /// Unlocks TTS and flushes queued messages.
    /// </summary>
    private async Task HandleDictationCompleteAsync()
    {
        _logger.LogInformation("Dictation complete - unlocking TTS and flushing queue (queue count: {QueueCount})",
            _queueInfo.QueueCount);

        // Unlock TTS to allow playback
        _lockService.Unlock();

        // Flush queued messages (FIFO order)
        if (_queueInfo.QueueCount > 0)
        {
            _logger.LogInformation("Flushing {Count} queued TTS messages", _queueInfo.QueueCount);
            await _speechCancellation.FlushQueueAsync();
        }
    }

    /// <summary>
    /// Stops the coordinator and unsubscribes from state changes.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DictationSpeechCoordinator stopping - unsubscribing from state changes");

        // Unsubscribe from state changes
        _stateMachine.StateChanged -= OnDictationStateChanged;

        _logger.LogInformation("DictationSpeechCoordinator stopped");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _stateMachine.StateChanged -= OnDictationStateChanged;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
