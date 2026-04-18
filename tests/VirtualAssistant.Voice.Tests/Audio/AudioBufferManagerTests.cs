using Olbrasoft.VirtualAssistant.Voice.Audio;

namespace Olbrasoft.VirtualAssistant.Voice.Tests.Audio;

/// <summary>
/// Unit tests for <see cref="AudioBufferManager"/>. This state used to be two
/// private fields inside AudioRecordingCoordinator and was only covered by the
/// coordinator's end-to-end tests — now that it is its own class, the append,
/// copy-tail, drain, and size-limit branches each have focused coverage.
/// </summary>
public class AudioBufferManagerTests
{
    [Fact]
    public void TryAppend_WithinLimit_AppendsAndReportsNewCount()
    {
        var sut = new AudioBufferManager();

        var appended = sut.TryAppend(new byte[] { 1, 2, 3 }, maxSizeBytes: 100, out var newCount);

        Assert.True(appended);
        Assert.Equal(3, newCount);
        Assert.Equal(3, sut.ByteCount);
    }

    [Fact]
    public void TryAppend_ExceedingLimit_ReturnsFalseWithoutMutating()
    {
        var sut = new AudioBufferManager();
        sut.TryAppend(new byte[] { 1, 2, 3 }, maxSizeBytes: 10, out _);

        var appended = sut.TryAppend(new byte[] { 4, 5, 6, 7, 8, 9, 10, 11 }, maxSizeBytes: 10, out var newCount);

        // Legacy behavior: rejected append does not partially write — byte count
        // stays at 3 so the capture loop can log and break without leaving a
        // half-appended chunk in the buffer.
        Assert.False(appended);
        Assert.Equal(3, newCount);
        Assert.Equal(3, sut.ByteCount);
    }

    [Fact]
    public void CopyTail_SnapshotsFromCursorToEnd_AndReportsNewCursor()
    {
        var sut = new AudioBufferManager();
        sut.TryAppend(new byte[] { 1, 2, 3, 4, 5 }, maxSizeBytes: 100, out _);

        var tail = sut.CopyTail(fromCursor: 2, out var newCursor);

        Assert.Equal(new byte[] { 3, 4, 5 }, tail);
        Assert.Equal(5, newCursor);
        Assert.Equal(5, sut.ByteCount);
    }

    [Fact]
    public void CopyTail_CursorAtEnd_ReturnsEmptyArray()
    {
        var sut = new AudioBufferManager();
        sut.TryAppend(new byte[] { 1, 2, 3 }, maxSizeBytes: 100, out _);

        var tail = sut.CopyTail(fromCursor: 3, out var newCursor);

        Assert.Empty(tail);
        Assert.Equal(3, newCursor);
    }

    [Fact]
    public void DrainToArray_ReturnsCopyAndClears()
    {
        var sut = new AudioBufferManager();
        sut.TryAppend(new byte[] { 1, 2, 3 }, maxSizeBytes: 100, out _);

        var drained = sut.DrainToArray();

        Assert.Equal(new byte[] { 1, 2, 3 }, drained);
        Assert.Equal(0, sut.ByteCount);

        // Mutating the drained array must not affect the buffer's next session.
        drained[0] = 99;
        Assert.Equal(0, sut.ByteCount);
    }

    [Fact]
    public void Clear_ResetsByteCount()
    {
        var sut = new AudioBufferManager();
        sut.TryAppend(new byte[] { 1, 2, 3 }, maxSizeBytes: 100, out _);

        sut.Clear();

        Assert.Equal(0, sut.ByteCount);
    }
}
