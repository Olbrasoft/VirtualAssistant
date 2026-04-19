using Olbrasoft.VirtualAssistant.Core.Audio;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Voice.StateMachine;

namespace Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

/// <inheritdoc />
public sealed class DictationRecordingSession : IDictationRecordingSession, IDisposable
{
    private readonly ILogger<DictationRecordingSession> _logger;
    private readonly IAudioRecordingCoordinator _recordingCoordinator;
    private readonly IDictationTranscriber _transcriber;
    private readonly IDictationStateMachine _stateMachine;

    public DictationRecordingSession(
        ILogger<DictationRecordingSession> logger,
        IAudioRecordingCoordinator recordingCoordinator,
        IDictationTranscriber transcriber,
        IDictationStateMachine stateMachine)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _recordingCoordinator = recordingCoordinator ?? throw new ArgumentNullException(nameof(recordingCoordinator));
        _transcriber = transcriber ?? throw new ArgumentNullException(nameof(transcriber));
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));

        // Forward each emitted chunk to the transcriber's streaming assembler.
        // Kept inside the session so the worker never sees the chunk event.
        _recordingCoordinator.ChunkAvailable += OnChunkAvailable;
    }

    public DictationState CurrentState => _stateMachine.CurrentState;

    public bool IsStreamingActive => _transcriber.IsStreamingActive;

    public async Task StartAsync(bool streamingActive)
    {
        try
        {
            _stateMachine.TransitionTo(DictationState.Recording);
            _transcriber.BeginSession(streamingActive);

            if (streamingActive)
            {
                _recordingCoordinator.EnableChunking(TimeSpan.FromSeconds(8));
                _logger.LogInformation("Streaming transcription active for this session (8s chunks)");
            }
            else
            {
                _recordingCoordinator.DisableChunking();
            }

            await _recordingCoordinator.StartRecordingAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start recording");
            _stateMachine.TransitionTo(DictationState.Idle);
        }
    }

    public async Task<byte[]?> StopAndValidateAsync()
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

    public Task EmergencyStopAsync() => _recordingCoordinator.EmergencyStopAsync();

    public void EndSession()
    {
        _transcriber.EndSession();
        _recordingCoordinator.DisableChunking();
    }

    private void OnChunkAvailable(object? sender, AudioChunkEventArgs e) =>
        _transcriber.ForwardChunk(e.Index, e.PcmBytes);

    public void Dispose()
    {
        _recordingCoordinator.ChunkAvailable -= OnChunkAvailable;
    }
}
