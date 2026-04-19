using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Audio;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Service.Workers.Dictation;
using Olbrasoft.VirtualAssistant.Voice.StateMachine;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Workers.Dictation;

/// <summary>
/// Pins the recording-session façade lifted out of DictationWorker in #969:
/// StartAsync toggles chunking (8s on/off), opens the transcriber's streaming
/// session, and transitions the state machine to Recording; StopAndValidateAsync
/// stops the coordinator, transitions to Transcribing on non-empty buffers or
/// Idle on empty, and returns null on empty. Session also owns the
/// ChunkAvailable → transcriber.ForwardChunk forwarding.
/// </summary>
public class DictationRecordingSessionTests
{
    private readonly Mock<ILogger<DictationRecordingSession>> _loggerMock = new();
    private readonly Mock<IAudioRecordingCoordinator> _coordinatorMock = new();
    private readonly Mock<IDictationTranscriber> _transcriberMock = new();
    private readonly Mock<IDictationStateMachine> _stateMachineMock = new();

    private DictationRecordingSession CreateSut() =>
        new(_loggerMock.Object, _coordinatorMock.Object, _transcriberMock.Object, _stateMachineMock.Object);

    [Fact]
    public void CurrentState_DelegatesToStateMachine()
    {
        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Transcribing);
        var sut = CreateSut();

        Assert.Equal(DictationState.Transcribing, sut.CurrentState);
    }

    [Fact]
    public async Task StartAsync_Streaming_TransitionsRecordingEnablesChunkingAndOpensSession()
    {
        var sut = CreateSut();

        await sut.StartAsync(streamingActive: true);

        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Recording), Times.Once);
        _transcriberMock.Verify(x => x.BeginSession(true), Times.Once);
        _coordinatorMock.Verify(x => x.EnableChunking(TimeSpan.FromSeconds(8)), Times.Once);
        _coordinatorMock.Verify(x => x.DisableChunking(), Times.Never);
        _coordinatorMock.Verify(x => x.StartRecordingAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_NonStreaming_TransitionsRecordingDisablesChunkingAndOpensSession()
    {
        var sut = CreateSut();

        await sut.StartAsync(streamingActive: false);

        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Recording), Times.Once);
        _transcriberMock.Verify(x => x.BeginSession(false), Times.Once);
        _coordinatorMock.Verify(x => x.DisableChunking(), Times.Once);
        _coordinatorMock.Verify(x => x.EnableChunking(It.IsAny<TimeSpan>()), Times.Never);
        _coordinatorMock.Verify(x => x.StartRecordingAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_StartRecordingThrows_TransitionsBackToIdle()
    {
        _coordinatorMock
            .Setup(x => x.StartRecordingAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var sut = CreateSut();

        await sut.StartAsync(streamingActive: false);

        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Recording), Times.Once);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.Once);
    }

    [Fact]
    public async Task StopAndValidateAsync_NonEmptyBuffer_TransitionsTranscribingAndReturnsAudio()
    {
        var buffer = new byte[] { 1, 2, 3 };
        _coordinatorMock.Setup(x => x.StopRecordingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(buffer);
        var sut = CreateSut();

        var result = await sut.StopAndValidateAsync();

        Assert.Same(buffer, result);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Transcribing), Times.Once);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.Never);
    }

    [Fact]
    public async Task StopAndValidateAsync_EmptyBuffer_TransitionsIdleAndReturnsNull()
    {
        _coordinatorMock.Setup(x => x.StopRecordingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<byte>());
        var sut = CreateSut();

        var result = await sut.StopAndValidateAsync();

        Assert.Null(result);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.Once);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Transcribing), Times.Never);
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
            new DictationRecordingSession(null!, _coordinatorMock.Object, _transcriberMock.Object, _stateMachineMock.Object));

    [Fact]
    public void Constructor_NullCoordinator_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new DictationRecordingSession(_loggerMock.Object, null!, _transcriberMock.Object, _stateMachineMock.Object));

    [Fact]
    public void Constructor_NullTranscriber_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new DictationRecordingSession(_loggerMock.Object, _coordinatorMock.Object, null!, _stateMachineMock.Object));

    [Fact]
    public void Constructor_NullStateMachine_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new DictationRecordingSession(_loggerMock.Object, _coordinatorMock.Object, _transcriberMock.Object, null!));
}
