using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Voice.StateMachine;

namespace Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

/// <inheritdoc />
public sealed class DictationCancellationCoordinator : IDictationCancellationCoordinator, IDisposable
{
    private readonly ILogger<DictationCancellationCoordinator> _logger;
    private readonly IDictationStateMachine _stateMachine;
    private readonly IDictationRecordingSession _recordingSession;
    private readonly IDictationOutputChannel _outputChannel;
    private CancellationTokenSource? _transcriptionCts;

    public DictationCancellationCoordinator(
        ILogger<DictationCancellationCoordinator> logger,
        IDictationStateMachine stateMachine,
        IDictationRecordingSession recordingSession,
        IDictationOutputChannel outputChannel)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _recordingSession = recordingSession ?? throw new ArgumentNullException(nameof(recordingSession));
        _outputChannel = outputChannel ?? throw new ArgumentNullException(nameof(outputChannel));
    }

    public CancellationToken BeginTranscription()
    {
        // Idempotent while a prior transcription hasn't ended yet — we return
        // the existing token instead of disposing the CTS under the still-
        // running pipeline call, which would ObjectDisposedException on any
        // Register/CancelAfter/etc. the pipeline does with the token.
        // (Copilot review on PR #1036.)
        _transcriptionCts ??= new CancellationTokenSource();
        return _transcriptionCts.Token;
    }

    public void EndTranscription()
    {
        _transcriptionCts?.Dispose();
        _transcriptionCts = null;
    }

    public async Task CancelRecordingAsync()
    {
        try
        {
            _logger.LogInformation("Canceling recording");
            await _recordingSession.EmergencyStopAsync();
            _outputChannel.PlayCancelCue();
            _stateMachine.TransitionTo(DictationState.Idle);
            _logger.LogInformation("Recording canceled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error canceling recording");
            _stateMachine.TransitionTo(DictationState.Idle);
        }
        finally
        {
            // End the streaming session so the chunk assembler doesn't keep
            // per-chunk transcription tasks running into the next session.
            // (Copilot review on PR #1036.)
            _recordingSession.EndSession();
        }
    }

    public async Task EmergencyStopAsync()
    {
        try
        {
            await _recordingSession.EmergencyStopAsync();
            _stateMachine.TransitionTo(DictationState.Idle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during emergency stop");
            _stateMachine.TransitionTo(DictationState.Idle);
        }
        finally
        {
            _recordingSession.EndSession();
        }
    }

    public void CancelTranscription()
    {
        try
        {
            var currentState = _stateMachine.CurrentState;
            _logger.LogInformation("CancelTranscription called in state {State}", currentState);

            _outputChannel.StopTypingFeedback();
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
                // infrequent cancel path, not the transcription/streaming hot
                // path.
                _recordingSession.EmergencyStopAsync().GetAwaiter().GetResult();
            }

            _transcriptionCts?.Cancel();
            _transcriptionCts?.Dispose();
            _transcriptionCts = null;

            // Cancel in-flight streaming chunk tasks and clear chunk state,
            // otherwise they keep running into the next session.
            _recordingSession.EndSession();

            _stateMachine.TransitionTo(DictationState.Idle);
            _logger.LogInformation("Dictation canceled from state {State}", currentState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error canceling transcription");
        }
    }

    public async Task ShutdownAsync()
    {
        _logger.LogInformation("Stopping recording on worker shutdown");
        try
        {
            await _recordingSession.EmergencyStopAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error stopping recording on shutdown");
        }

        _stateMachine.TransitionTo(DictationState.Idle);
    }

    public void Dispose()
    {
        _transcriptionCts?.Dispose();
        _transcriptionCts = null;
    }
}
