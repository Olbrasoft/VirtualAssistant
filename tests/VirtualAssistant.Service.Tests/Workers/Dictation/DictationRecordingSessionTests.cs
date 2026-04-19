using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Audio;
using Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Workers.Dictation;

/// <summary>
/// Pins the recording-session façade lifted out of DictationWorker in #969:
/// StartAsync toggles chunking (8s on/off) and opens the transcriber's
/// streaming session before starting audio capture, EmergencyStop discards
/// audio without side-effects, and ChunkAvailable events are forwarded
/// internally to the transcriber so the worker never sees them.
/// </summary>
public class DictationRecordingSessionTests
{
    private readonly Mock<ILogger<DictationRecordingSession>> _loggerMock = new();
    private readonly Mock<IAudioRecordingCoordinator> _coordinatorMock = new();
    private readonly Mock<IDictationTranscriber> _transcriberMock = new();

    private DictationRecordingSession CreateSut() =>
        new(_loggerMock.Object, _coordinatorMock.Object, _transcriberMock.Object);

    [Fact]
    public async Task StartAsync_Streaming_EnablesChunkingAndOpensTranscriberSession()
    {
        var sut = CreateSut();

        await sut.StartAsync(streamingActive: true);

        _transcriberMock.Verify(x => x.BeginSession(true), Times.Once);
        _coordinatorMock.Verify(x => x.EnableChunking(TimeSpan.FromSeconds(8)), Times.Once);
        _coordinatorMock.Verify(x => x.DisableChunking(), Times.Never);
        _coordinatorMock.Verify(x => x.StartRecordingAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_NonStreaming_DisablesChunkingAndStillOpensTranscriberSession()
    {
        // Non-streaming is the default path; the transcriber still gets a
        // BeginSession(false) so the assembler's "is active" flag is reset
        // from any prior streaming session.
        var sut = CreateSut();

        await sut.StartAsync(streamingActive: false);

        _transcriberMock.Verify(x => x.BeginSession(false), Times.Once);
        _coordinatorMock.Verify(x => x.DisableChunking(), Times.Once);
        _coordinatorMock.Verify(x => x.EnableChunking(It.IsAny<TimeSpan>()), Times.Never);
        _coordinatorMock.Verify(x => x.StartRecordingAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StopAsync_ReturnsCoordinatorBuffer()
    {
        var buffer = new byte[] { 1, 2, 3 };
        _coordinatorMock.Setup(x => x.StopRecordingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(buffer);
        var sut = CreateSut();

        var result = await sut.StopAsync();

        Assert.Same(buffer, result);
    }

    [Fact]
    public async Task EmergencyStopAsync_ForwardsToCoordinator()
    {
        _coordinatorMock.Setup(x => x.EmergencyStopAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        await sut.EmergencyStopAsync();

        _coordinatorMock.Verify(x => x.EmergencyStopAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void EndSession_EndsTranscriberSessionAndDisablesChunking()
    {
        var sut = CreateSut();

        sut.EndSession();

        _transcriberMock.Verify(x => x.EndSession(), Times.Once);
        _coordinatorMock.Verify(x => x.DisableChunking(), Times.Once);
    }

    [Fact]
    public void IsStreamingActive_MirrorsTranscriber()
    {
        _transcriberMock.SetupGet(x => x.IsStreamingActive).Returns(true);
        var sut = CreateSut();

        Assert.True(sut.IsStreamingActive);
    }

    [Fact]
    public void ChunkAvailable_ForwardedToTranscriber()
    {
        // The point of pulling this into the session is that the worker never
        // sees ChunkAvailable — the session subscribes at construction and
        // fans out to the transcriber internally.
        var sut = CreateSut();

        _coordinatorMock.Raise(x => x.ChunkAvailable += null,
            this, new AudioChunkEventArgs(3, new byte[] { 1, 2 }));

        _transcriberMock.Verify(
            x => x.ForwardChunk(3, It.Is<byte[]>(b => b.Length == 2)),
            Times.Once);
    }

    [Fact]
    public void Dispose_UnsubscribesFromChunkAvailable()
    {
        var sut = CreateSut();

        sut.Dispose();
        _coordinatorMock.Raise(x => x.ChunkAvailable += null,
            this, new AudioChunkEventArgs(0, new byte[] { 1 }));

        _transcriberMock.Verify(x => x.ForwardChunk(It.IsAny<int>(), It.IsAny<byte[]>()), Times.Never);
    }

    [Fact]
    public void Constructor_NullLogger_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new DictationRecordingSession(null!, _coordinatorMock.Object, _transcriberMock.Object));

    [Fact]
    public void Constructor_NullCoordinator_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new DictationRecordingSession(_loggerMock.Object, null!, _transcriberMock.Object));

    [Fact]
    public void Constructor_NullTranscriber_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new DictationRecordingSession(_loggerMock.Object, _coordinatorMock.Object, null!));
}
