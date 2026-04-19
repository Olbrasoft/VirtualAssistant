using Olbrasoft.VirtualAssistant.Core.Audio;

namespace Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

/// <inheritdoc />
public sealed class DictationRecordingSession : IDictationRecordingSession, IDisposable
{
    private readonly ILogger<DictationRecordingSession> _logger;
    private readonly IAudioRecordingCoordinator _recordingCoordinator;
    private readonly IDictationTranscriber _transcriber;

    public DictationRecordingSession(
        ILogger<DictationRecordingSession> logger,
        IAudioRecordingCoordinator recordingCoordinator,
        IDictationTranscriber transcriber)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _recordingCoordinator = recordingCoordinator ?? throw new ArgumentNullException(nameof(recordingCoordinator));
        _transcriber = transcriber ?? throw new ArgumentNullException(nameof(transcriber));

        // Forward each emitted chunk to the transcriber's streaming assembler.
        // Kept inside the session so the worker never sees the chunk event.
        _recordingCoordinator.ChunkAvailable += OnChunkAvailable;
    }

    public bool IsStreamingActive => _transcriber.IsStreamingActive;

    public async Task StartAsync(bool streamingActive)
    {
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

    public Task<byte[]> StopAsync() => _recordingCoordinator.StopRecordingAsync();

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
