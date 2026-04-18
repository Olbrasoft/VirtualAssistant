using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.StateMachine;

namespace Olbrasoft.VirtualAssistant.Core.Tests.StateMachine;

/// <summary>
/// Pins the VoiceStateMachine transition rules — no test previously existed
/// for this class even though several worker subscribers key off StateChanged
/// to drive the recording / listening UI. Cases cover the public transition
/// helpers (TransitionTo, StartRecording, ResetToWaiting, ResetToMuted) plus
/// the event firing rules that distinguish no-op transitions from real ones.
/// </summary>
public class VoiceStateMachineTests
{
    private readonly Mock<ILogger<VoiceStateMachine>> _loggerMock = new();

    private VoiceStateMachine CreateSut(bool startMuted = false) =>
        new(_loggerMock.Object, startMuted);

    [Fact]
    public void Ctor_DefaultStartState_IsWaiting()
    {
        var sut = CreateSut();
        Assert.Equal(VoiceState.Waiting, sut.CurrentState);
    }

    [Fact]
    public void Ctor_StartMutedTrue_StartsInMutedState()
    {
        var sut = CreateSut(startMuted: true);
        Assert.Equal(VoiceState.Muted, sut.CurrentState);
    }

    [Fact]
    public void TransitionTo_NewState_FiresStateChangedWithPrevAndNew()
    {
        var sut = CreateSut();
        VoiceStateChangedEventArgs? captured = null;
        sut.StateChanged += (_, e) => captured = e;

        sut.TransitionTo(VoiceState.Recording);

        Assert.Equal(VoiceState.Recording, sut.CurrentState);
        Assert.NotNull(captured);
        Assert.Equal(VoiceState.Waiting, captured!.PreviousState);
        Assert.Equal(VoiceState.Recording, captured.NewState);
    }

    [Fact]
    public void TransitionTo_SameState_DoesNotFireStateChanged()
    {
        var sut = CreateSut();
        var fired = 0;
        sut.StateChanged += (_, _) => fired++;

        sut.TransitionTo(VoiceState.Waiting);

        Assert.Equal(0, fired);
    }

    [Fact]
    public void StartRecording_SetsStateToRecordingAndCapturesStartTime()
    {
        var sut = CreateSut();
        var before = DateTime.UtcNow;

        sut.StartRecording(vadProbability: 0.87f);

        var after = DateTime.UtcNow;
        Assert.Equal(VoiceState.Recording, sut.CurrentState);
        Assert.InRange(sut.RecordingStartTime, before, after);
    }

    [Fact]
    public void StartRecording_AlwaysEmitsTransitionFromWaiting()
    {
        // StartRecording is a specialized transition helper that unconditionally
        // reports the previous state as Waiting — the event needs to fire even
        // from Muted so downstream subscribers refresh the UI correctly.
        var sut = CreateSut(startMuted: true);
        VoiceStateChangedEventArgs? captured = null;
        sut.StateChanged += (_, e) => captured = e;

        sut.StartRecording(0.5f);

        Assert.NotNull(captured);
        Assert.Equal(VoiceState.Waiting, captured!.PreviousState);
        Assert.Equal(VoiceState.Recording, captured.NewState);
    }

    [Fact]
    public void ResetToWaiting_FromRecording_FiresStateChanged()
    {
        var sut = CreateSut();
        sut.StartRecording(0.9f);
        VoiceStateChangedEventArgs? captured = null;
        sut.StateChanged += (_, e) => captured = e;

        sut.ResetToWaiting();

        Assert.Equal(VoiceState.Waiting, sut.CurrentState);
        Assert.NotNull(captured);
        Assert.Equal(VoiceState.Recording, captured!.PreviousState);
        Assert.Equal(VoiceState.Waiting, captured.NewState);
    }

    [Fact]
    public void ResetToWaiting_AlreadyWaiting_DoesNotFireEvent()
    {
        var sut = CreateSut();
        var fired = 0;
        sut.StateChanged += (_, _) => fired++;

        sut.ResetToWaiting();

        Assert.Equal(0, fired);
    }

    [Fact]
    public void ResetToMuted_FromRecording_FiresAndClearsTimers()
    {
        var sut = CreateSut();
        sut.StartRecording(0.5f);
        sut.SilenceStartTime = DateTime.UtcNow;
        VoiceStateChangedEventArgs? captured = null;
        sut.StateChanged += (_, e) => captured = e;

        sut.ResetToMuted();

        Assert.Equal(VoiceState.Muted, sut.CurrentState);
        Assert.Equal(default, sut.SilenceStartTime);
        Assert.Equal(default, sut.RecordingStartTime);
        Assert.NotNull(captured);
        Assert.Equal(VoiceState.Recording, captured!.PreviousState);
        Assert.Equal(VoiceState.Muted, captured.NewState);
    }

    [Fact]
    public void ResetToMuted_AlreadyMuted_DoesNotFireEvent()
    {
        var sut = CreateSut(startMuted: true);
        var fired = 0;
        sut.StateChanged += (_, _) => fired++;

        sut.ResetToMuted();

        Assert.Equal(0, fired);
    }

    [Fact]
    public void SilenceStartTime_GetSet_RoundTripsThroughLock()
    {
        var sut = CreateSut();
        var timestamp = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc);

        sut.SilenceStartTime = timestamp;

        Assert.Equal(timestamp, sut.SilenceStartTime);
    }
}
