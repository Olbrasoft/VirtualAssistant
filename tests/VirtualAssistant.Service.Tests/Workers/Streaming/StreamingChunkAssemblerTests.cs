using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Service.Workers.Streaming;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Workers.Streaming;

/// <summary>
/// Pins the extraction contract lifted out of DictationWorker in PR #1021:
/// Reset(false) disables submissions, ordered combine respects chunk index,
/// CancelAndClear stops in-flight work, and the null-return paths fire when
/// the assembler is idle or empty. Copilot's post-merge review on #1021
/// flagged that the new class had no focused unit tests.
/// </summary>
public class StreamingChunkAssemblerTests
{
    private readonly Mock<ILogger<StreamingChunkAssembler>> _loggerMock = new();
    private readonly Mock<ITranscriptionService> _transcriptionMock = new();

    private StreamingChunkAssembler CreateSut() => new(_loggerMock.Object, _transcriptionMock.Object);

    [Fact]
    public void Reset_False_MakesSubmitChunk_ANoOp()
    {
        var sut = CreateSut();
        sut.Reset(active: false);

        sut.SubmitChunk(0, new byte[] { 1, 2, 3 });

        Assert.False(sut.IsActive);
        Assert.Equal(0, sut.CompletedChunkCount);
        _transcriptionMock.Verify(
            x => x.TranscribeChunkRawAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Reset_True_EnablesSubmitChunk()
    {
        var sut = CreateSut();
        _transcriptionMock
            .Setup(x => x.TranscribeChunkRawAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ok");

        sut.Reset(active: true);
        sut.SubmitChunk(0, new byte[] { 1 });

        Assert.True(sut.IsActive);
    }

    [Fact]
    public async Task CombineAsync_CombinesChunksInIndexOrder_RegardlessOfSubmissionOrder()
    {
        var sut = CreateSut();
        var perChunk = new Dictionary<byte, string>
        {
            [0] = "first",
            [1] = "second",
            [2] = "third",
        };
        _transcriptionMock
            .Setup(x => x.TranscribeChunkRawAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns<byte[], CancellationToken>((bytes, _) => Task.FromResult(perChunk[bytes[0]]));

        sut.Reset(active: true);
        sut.SubmitChunk(2, new byte[] { 2 });
        sut.SubmitChunk(0, new byte[] { 0 });
        sut.SubmitChunk(1, new byte[] { 1 });

        var combined = await sut.CombineAsync(CancellationToken.None);

        Assert.Equal("first second third", combined);
        Assert.Equal(3, sut.CompletedChunkCount);
    }

    [Fact]
    public async Task CombineAsync_CollapsesInteriorWhitespaceAndTrims()
    {
        var sut = CreateSut();
        _transcriptionMock
            .Setup(x => x.TranscribeChunkRawAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("  hello   world  ");

        sut.Reset(active: true);
        sut.SubmitChunk(0, new byte[] { 1 });

        var combined = await sut.CombineAsync(CancellationToken.None);

        Assert.Equal("hello world", combined);
    }

    [Fact]
    public async Task CombineAsync_WhenInactive_ReturnsNull()
    {
        var sut = CreateSut();

        var combined = await sut.CombineAsync(CancellationToken.None);

        Assert.Null(combined);
    }

    [Fact]
    public async Task CombineAsync_WhenActiveWithNoChunks_ReturnsNull()
    {
        var sut = CreateSut();
        sut.Reset(active: true);

        var combined = await sut.CombineAsync(CancellationToken.None);

        Assert.Null(combined);
    }

    [Fact]
    public async Task CancelAndClear_CancelsInFlightTasks_AndClearsState()
    {
        // Force the chunk task to sit on ct.WaitHandle so we can deterministically
        // observe the cancellation path without depending on wall-clock delays.
        var sut = CreateSut();
        var enteredHandler = new TaskCompletionSource();
        _transcriptionMock
            .Setup(x => x.TranscribeChunkRawAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns<byte[], CancellationToken>(async (_, ct) =>
            {
                enteredHandler.TrySetResult();
                await Task.Delay(Timeout.Infinite, ct);
                return "unreachable";
            });

        sut.Reset(active: true);
        sut.SubmitChunk(0, new byte[] { 1 });
        await enteredHandler.Task;

        sut.CancelAndClear();

        Assert.False(sut.IsActive);
        Assert.Equal(0, sut.CompletedChunkCount);

        // Combine on the now-idle assembler returns null — state is fully reset.
        var combined = await sut.CombineAsync(CancellationToken.None);
        Assert.Null(combined);
    }

    [Fact]
    public async Task SubmitChunk_AfterCancelAndClear_DoesNotInvokeTranscription()
    {
        var sut = CreateSut();
        sut.Reset(active: true);
        sut.CancelAndClear();

        sut.SubmitChunk(0, new byte[] { 1 });
        await Task.Yield();

        _transcriptionMock.Verify(
            x => x.TranscribeChunkRawAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CombineAsync_FaultedChunk_DoesNotThrow_AndReturnsSurvivingChunks()
    {
        var sut = CreateSut();
        _transcriptionMock
            .Setup(x => x.TranscribeChunkRawAsync(It.Is<byte[]>(b => b[0] == 0), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        _transcriptionMock
            .Setup(x => x.TranscribeChunkRawAsync(It.Is<byte[]>(b => b[0] == 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync("survivor");

        sut.Reset(active: true);
        sut.SubmitChunk(0, new byte[] { 0 });
        sut.SubmitChunk(1, new byte[] { 1 });

        var combined = await sut.CombineAsync(CancellationToken.None);

        // Faulted chunk is recorded as empty string, surviving chunk contributes
        // its text; Where(t => t.Length > 0) drops the empty entry cleanly.
        Assert.Equal("survivor", combined);
    }

    [Fact]
    public async Task Reset_True_AfterPriorSession_ClearsCompletedCount()
    {
        // Pins the across-sessions reuse contract the xmldoc describes: the
        // assembler is a singleton reused across sessions, so Reset must
        // discard the prior session's results.
        var sut = CreateSut();
        _transcriptionMock
            .Setup(x => x.TranscribeChunkRawAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("x");

        sut.Reset(active: true);
        sut.SubmitChunk(0, new byte[] { 1 });
        await sut.CombineAsync(CancellationToken.None);
        Assert.Equal(1, sut.CompletedChunkCount);

        sut.Reset(active: true);

        Assert.Equal(0, sut.CompletedChunkCount);
    }
}
