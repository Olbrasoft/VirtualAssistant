using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Audio;
using Olbrasoft.VirtualAssistant.Voice.Audio;

namespace Olbrasoft.VirtualAssistant.Voice.Tests.Audio;

/// <summary>
/// Unit tests for <see cref="ChunkEmissionScheduler"/>. Verifies the
/// enable/interval/cursor state machine that used to live inline in
/// AudioRecordingCoordinator.
/// </summary>
public class ChunkEmissionSchedulerTests
{
    private readonly ChunkEmissionScheduler _sut = new(Mock.Of<ILogger<ChunkEmissionScheduler>>());

    [Fact]
    public void TryEmitIfDue_WhenDisabled_DoesNothing()
    {
        var buffer = new AudioBufferManager();
        buffer.TryAppend(new byte[] { 1, 2, 3 }, maxSizeBytes: 100, out _);
        var raised = 0;
        _sut.ChunkAvailable += (_, _) => raised++;

        _sut.TryEmitIfDue(buffer);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void TryEmitIfDue_EnabledButIntervalNotElapsed_DoesNothing()
    {
        // Reset sets lastEmit = UtcNow; a 10s interval means the very next call
        // is definitely inside the dead zone, so nothing should fire.
        _sut.Enable(TimeSpan.FromSeconds(10));
        _sut.Reset();
        var buffer = new AudioBufferManager();
        buffer.TryAppend(new byte[] { 1, 2, 3 }, maxSizeBytes: 100, out _);
        var raised = 0;
        _sut.ChunkAvailable += (_, _) => raised++;

        _sut.TryEmitIfDue(buffer);

        Assert.Equal(0, raised);
    }

    [Fact]
    public async Task TryEmitIfDue_EnabledAndIntervalElapsed_EmitsChunkWithAdvancingIndex()
    {
        // 1-s floor is enforced by Enable; use the floor to keep the test fast.
        _sut.Enable(TimeSpan.FromMilliseconds(1));
        _sut.Reset();
        var buffer = new AudioBufferManager();
        buffer.TryAppend(new byte[] { 1, 2, 3 }, maxSizeBytes: 100, out _);
        var received = new List<AudioChunkEventArgs>();
        _sut.ChunkAvailable += (_, e) => received.Add(e);

        await Task.Delay(1100); // step past the 1-s floor
        _sut.TryEmitIfDue(buffer);

        buffer.TryAppend(new byte[] { 4, 5 }, maxSizeBytes: 100, out _);
        await Task.Delay(1100);
        _sut.TryEmitIfDue(buffer);

        Assert.Equal(2, received.Count);
        Assert.Equal(0, received[0].Index);
        Assert.Equal(new byte[] { 1, 2, 3 }, received[0].PcmBytes);
        Assert.Equal(1, received[1].Index);
        Assert.Equal(new byte[] { 4, 5 }, received[1].PcmBytes);
    }

    [Fact]
    public async Task TryEmitIfDue_NoNewBytesSinceLastEmit_DoesNotEmit()
    {
        _sut.Enable(TimeSpan.FromMilliseconds(1));
        _sut.Reset();
        var buffer = new AudioBufferManager();
        buffer.TryAppend(new byte[] { 1, 2, 3 }, maxSizeBytes: 100, out _);
        var raised = 0;
        _sut.ChunkAvailable += (_, _) => raised++;

        await Task.Delay(1100);
        _sut.TryEmitIfDue(buffer); // first emit consumes all 3 bytes

        await Task.Delay(1100);
        _sut.TryEmitIfDue(buffer); // no new bytes → skip

        Assert.Equal(1, raised);
    }

    [Fact]
    public void EmitFinal_EnabledWithPendingBytes_EmitsRegardlessOfInterval()
    {
        // EmitFinal is the "flush tail" call on stop. It must bypass the
        // interval check so the final transcription chunk isn't lost.
        _sut.Enable(TimeSpan.FromMinutes(30));
        _sut.Reset();
        var buffer = new AudioBufferManager();
        buffer.TryAppend(new byte[] { 7, 8, 9 }, maxSizeBytes: 100, out _);
        AudioChunkEventArgs? received = null;
        _sut.ChunkAvailable += (_, e) => received = e;

        _sut.EmitFinal(buffer);

        Assert.NotNull(received);
        Assert.Equal(new byte[] { 7, 8, 9 }, received.PcmBytes);
    }

    [Fact]
    public void EmitFinal_NoPendingBytes_DoesNotEmit()
    {
        _sut.Enable(TimeSpan.FromSeconds(1));
        _sut.Reset();
        var buffer = new AudioBufferManager();
        var raised = 0;
        _sut.ChunkAvailable += (_, _) => raised++;

        _sut.EmitFinal(buffer);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void Enable_IntervalBelowOneSecond_ClampsToOneSecond()
    {
        // Implementation detail worth pinning with a test: sub-second intervals
        // would thrash the capture loop, so Enable floors to 1 s. The
        // legacy code did this and callers rely on it.
        _sut.Enable(TimeSpan.FromMilliseconds(50));
        _sut.Reset();
        var buffer = new AudioBufferManager();
        buffer.TryAppend(new byte[] { 1 }, maxSizeBytes: 100, out _);
        var raised = 0;
        _sut.ChunkAvailable += (_, _) => raised++;

        // Without the 1-s floor this would fire immediately; with the floor it
        // is still inside the interval dead zone.
        _sut.TryEmitIfDue(buffer);
        Assert.Equal(0, raised);
    }

    [Fact]
    public async Task Disable_DuringSession_StopsFurtherEmissions()
    {
        _sut.Enable(TimeSpan.FromMilliseconds(1));
        _sut.Reset();
        var buffer = new AudioBufferManager();
        buffer.TryAppend(new byte[] { 1, 2 }, maxSizeBytes: 100, out _);
        var raised = 0;
        _sut.ChunkAvailable += (_, _) => raised++;

        await Task.Delay(1100);
        _sut.TryEmitIfDue(buffer); // 1 emit

        _sut.Disable();
        buffer.TryAppend(new byte[] { 3 }, maxSizeBytes: 100, out _);
        await Task.Delay(1100);
        _sut.TryEmitIfDue(buffer); // skipped

        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task SubscriberException_IsSwallowed_AndDoesNotBreakFutureEmits()
    {
        _sut.Enable(TimeSpan.FromMilliseconds(1));
        _sut.Reset();
        var buffer = new AudioBufferManager();
        buffer.TryAppend(new byte[] { 1 }, maxSizeBytes: 100, out _);

        var throwingHandler = new EventHandler<AudioChunkEventArgs>((_, _) => throw new InvalidOperationException("boom"));
        var receivedAfterThrow = 0;
        _sut.ChunkAvailable += throwingHandler;
        _sut.ChunkAvailable += (_, _) => receivedAfterThrow++;

        await Task.Delay(1100);
        _sut.TryEmitIfDue(buffer);

        // A subscriber exception must not propagate into the capture loop —
        // an earlier refactor that removed the try/catch around Invoke caused
        // the capture task to die silently. This test pins the contract.
        _sut.ChunkAvailable -= throwingHandler;
        buffer.TryAppend(new byte[] { 2 }, maxSizeBytes: 100, out _);
        await Task.Delay(1100);
        _sut.TryEmitIfDue(buffer);

        Assert.Equal(1, receivedAfterThrow);
    }
}
