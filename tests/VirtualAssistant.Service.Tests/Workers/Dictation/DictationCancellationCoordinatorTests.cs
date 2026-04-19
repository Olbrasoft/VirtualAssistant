using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Service.Workers.Dictation;
using Olbrasoft.VirtualAssistant.Voice.StateMachine;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Workers.Dictation;

/// <summary>
/// Pins the cancel/emergency-stop orchestration lifted out of DictationWorker
/// in #969. Covers the four teardown paths (Pause-while-recording, dictation-
/// disabled-while-active, user CancelTranscription, worker shutdown) plus the
/// transcription CTS lifecycle.
/// </summary>
public class DictationCancellationCoordinatorTests
{
    private readonly Mock<ILogger<DictationCancellationCoordinator>> _loggerMock = new();
    private readonly Mock<IDictationStateMachine> _stateMachineMock = new();
    private readonly Mock<IDictationRecordingSession> _recordingSessionMock = new();
    private readonly Mock<IDictationOutputChannel> _outputChannelMock = new();

    private DictationCancellationCoordinator CreateSut() =>
        new(_loggerMock.Object, _stateMachineMock.Object, _recordingSessionMock.Object, _outputChannelMock.Object);

    [Fact]
    public void BeginTranscription_ReturnsCancellableToken()
    {
        var sut = CreateSut();
        var token = sut.BeginTranscription();

        Assert.False(token.IsCancellationRequested);

        sut.CancelTranscription();
        Assert.True(token.IsCancellationRequested);
    }

    [Fact]
    public void BeginTranscription_Twice_WithoutEnd_ReturnsSameToken()
    {
        // Regression: two overlapping Begin calls (if two StopAndTranscribe
        // flows ever race past the state-machine guard in the worker) must
        // share the same CTS instead of disposing it under a still-running
        // pipeline call. (Copilot review on PR #1036.)
        var sut = CreateSut();
        var first = sut.BeginTranscription();
        var second = sut.BeginTranscription();

        Assert.Equal(first, second);
    }

    [Fact]
    public void BeginTranscription_AfterEnd_ReturnsFreshToken()
    {
        // The second Begin *does* rotate the CTS after the first finishes via
        // EndTranscription — otherwise cancel semantics would leak across
        // sessions.
        var sut = CreateSut();
        var first = sut.BeginTranscription();
        sut.EndTranscription();
        var second = sut.BeginTranscription();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void EndTranscription_DisposesCurrentCts()
    {
        var sut = CreateSut();
        _ = sut.BeginTranscription();

        sut.EndTranscription();
        // No-op EndTranscription is safe — a second call must not throw.
        sut.EndTranscription();
    }

    [Fact]
    public async Task CancelRecordingAsync_EmergencyStopsPlaysCueAndIdles()
    {
        _recordingSessionMock.Setup(x => x.EmergencyStopAsync()).Returns(Task.CompletedTask);
        var sut = CreateSut();

        await sut.CancelRecordingAsync();

        _recordingSessionMock.Verify(x => x.EmergencyStopAsync(), Times.Once);
        _outputChannelMock.Verify(x => x.PlayCancelCue(), Times.Once);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.Once);
    }

    [Fact]
    public async Task CancelRecordingAsync_EndsStreamingSession()
    {
        // Regression: Pause-while-recording must reset streaming chunk state
        // alongside the emergency stop — otherwise per-chunk transcription
        // tasks can leak into the next session. (Copilot review on PR #1036.)
        _recordingSessionMock.Setup(x => x.EmergencyStopAsync()).Returns(Task.CompletedTask);
        var sut = CreateSut();

        await sut.CancelRecordingAsync();

        _recordingSessionMock.Verify(x => x.EndSession(), Times.Once);
    }

    [Fact]
    public async Task CancelRecordingAsync_EmergencyStopThrows_StillTransitionsIdle()
    {
        _recordingSessionMock.Setup(x => x.EmergencyStopAsync()).ThrowsAsync(new InvalidOperationException());
        var sut = CreateSut();

        await sut.CancelRecordingAsync();

        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.Once);
    }

    [Fact]
    public async Task EmergencyStopAsync_EmergencyStopsIdlesAndEndsSession()
    {
        _recordingSessionMock.Setup(x => x.EmergencyStopAsync()).Returns(Task.CompletedTask);
        var sut = CreateSut();

        await sut.EmergencyStopAsync();

        _recordingSessionMock.Verify(x => x.EmergencyStopAsync(), Times.Once);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.Once);
        _recordingSessionMock.Verify(x => x.EndSession(), Times.Once);
    }

    [Fact]
    public async Task EmergencyStopAsync_NoCancelCue()
    {
        // Programmatic disable path — no user-facing sound.
        _recordingSessionMock.Setup(x => x.EmergencyStopAsync()).Returns(Task.CompletedTask);
        var sut = CreateSut();

        await sut.EmergencyStopAsync();

        _outputChannelMock.Verify(x => x.PlayCancelCue(), Times.Never);
    }

    [Fact]
    public void CancelTranscription_WhileTranscribing_StopsFeedbackPlaysCueAndIdles()
    {
        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Transcribing);
        var sut = CreateSut();
        _ = sut.BeginTranscription();

        sut.CancelTranscription();

        _outputChannelMock.Verify(x => x.StopTypingFeedback(), Times.Once);
        _outputChannelMock.Verify(x => x.PlayCancelCue(), Times.Once);
        _recordingSessionMock.Verify(x => x.EndSession(), Times.Once);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.Once);
    }

    [Fact]
    public void CancelTranscription_WhileRecording_AlsoEmergencyStopsRecording()
    {
        // Pause-while-recording → hub CancelTranscription path: must also tear
        // down the active recording before the state machine goes Idle.
        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingSessionMock.Setup(x => x.EmergencyStopAsync()).Returns(Task.CompletedTask);
        var sut = CreateSut();

        sut.CancelTranscription();

        _recordingSessionMock.Verify(x => x.EmergencyStopAsync(), Times.Once);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.Once);
    }

    [Fact]
    public void CancelTranscription_CancelsAndDisposesTheCts()
    {
        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Transcribing);
        var sut = CreateSut();
        var token = sut.BeginTranscription();

        sut.CancelTranscription();

        Assert.True(token.IsCancellationRequested);
    }

    [Fact]
    public async Task ShutdownAsync_EmergencyStopsAndIdles()
    {
        _recordingSessionMock.Setup(x => x.EmergencyStopAsync()).Returns(Task.CompletedTask);
        var sut = CreateSut();

        await sut.ShutdownAsync();

        _recordingSessionMock.Verify(x => x.EmergencyStopAsync(), Times.Once);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.Once);
    }

    [Fact]
    public async Task ShutdownAsync_NoSoundOrEndSession()
    {
        // Process-exit path — no cancel cue, no EndSession reset (nothing left
        // to reset since we're about to die).
        _recordingSessionMock.Setup(x => x.EmergencyStopAsync()).Returns(Task.CompletedTask);
        var sut = CreateSut();

        await sut.ShutdownAsync();

        _outputChannelMock.Verify(x => x.PlayCancelCue(), Times.Never);
        _recordingSessionMock.Verify(x => x.EndSession(), Times.Never);
    }

    [Fact]
    public async Task ShutdownAsync_EmergencyStopThrows_StillTransitionsIdle()
    {
        _recordingSessionMock.Setup(x => x.EmergencyStopAsync()).ThrowsAsync(new IOException());
        var sut = CreateSut();

        await sut.ShutdownAsync();

        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.Once);
    }

    [Fact]
    public void Constructor_NullLogger_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new DictationCancellationCoordinator(null!, _stateMachineMock.Object, _recordingSessionMock.Object, _outputChannelMock.Object));

    [Fact]
    public void Constructor_NullStateMachine_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new DictationCancellationCoordinator(_loggerMock.Object, null!, _recordingSessionMock.Object, _outputChannelMock.Object));

    [Fact]
    public void Constructor_NullRecordingSession_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new DictationCancellationCoordinator(_loggerMock.Object, _stateMachineMock.Object, null!, _outputChannelMock.Object));

    [Fact]
    public void Constructor_NullOutputChannel_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new DictationCancellationCoordinator(_loggerMock.Object, _stateMachineMock.Object, _recordingSessionMock.Object, null!));
}
