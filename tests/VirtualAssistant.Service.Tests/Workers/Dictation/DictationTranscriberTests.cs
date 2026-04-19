using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Service.Workers.Dictation;
using Olbrasoft.VirtualAssistant.Service.Workers.Streaming;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Workers.Dictation;

/// <summary>
/// Pins the transcription façade lifted out of DictationWorker in #969:
/// the raw path is streaming-aware (combined chunks when BeginSession(true)
/// and chunks were forwarded, full-buffer fallback when the combine was
/// empty), the full path is always full-buffer, and session lifecycle
/// methods forward cleanly to the underlying IStreamingChunkAssembler.
/// </summary>
public class DictationTranscriberTests
{
    private readonly Mock<ILogger<DictationTranscriber>> _loggerMock = new();
    private readonly Mock<ITranscriptionService> _transcriptionMock = new();
    private readonly Mock<IStreamingChunkAssembler> _assemblerMock = new();

    private DictationTranscriber CreateSut() =>
        new(_loggerMock.Object, _transcriptionMock.Object, _assemblerMock.Object);

    [Fact]
    public void BeginSession_ForwardsActiveFlagToAssembler()
    {
        var sut = CreateSut();
        sut.BeginSession(streamingActive: true);
        _assemblerMock.Verify(x => x.Reset(true), Times.Once);
    }

    [Fact]
    public void ForwardChunk_ForwardsToAssembler()
    {
        var sut = CreateSut();
        sut.ForwardChunk(7, new byte[] { 1, 2, 3 });
        _assemblerMock.Verify(x => x.SubmitChunk(7, It.Is<byte[]>(b => b.Length == 3)), Times.Once);
    }

    [Fact]
    public void EndSession_CancelsAssemblerState()
    {
        var sut = CreateSut();
        sut.EndSession();
        _assemblerMock.Verify(x => x.CancelAndClear(), Times.Once);
    }

    [Fact]
    public void IsStreamingActive_MirrorsAssemblerIsActive()
    {
        _assemblerMock.SetupGet(x => x.IsActive).Returns(true);
        var sut = CreateSut();

        Assert.True(sut.IsStreamingActive);
    }

    [Fact]
    public async Task TranscribeRawAsync_StreamingInactive_RunsFullBufferRaw()
    {
        _assemblerMock.SetupGet(x => x.IsActive).Returns(false);
        _transcriptionMock
            .Setup(x => x.TranscribeRawAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult("raw output", 0.9f));

        var sut = CreateSut();
        var result = await sut.TranscribeRawAsync(new byte[] { 1 }, CancellationToken.None);

        Assert.Equal("raw output", result.Text);
        _transcriptionMock.Verify(x => x.TranscribeRawAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        _assemblerMock.Verify(x => x.CombineAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TranscribeRawAsync_StreamingActive_WithCombinedText_FinalizesPretranscribed()
    {
        // The streaming path feeds the combined chunk output back through
        // FinalizePreTranscribedRawAsync so filters/corrections still apply —
        // the per-chunk Whisper output is just STT, not the final shape.
        _assemblerMock.SetupGet(x => x.IsActive).Returns(true);
        _assemblerMock.Setup(x => x.CombineAsync(It.IsAny<CancellationToken>())).ReturnsAsync("combined text");
        _transcriptionMock
            .Setup(x => x.FinalizePreTranscribedRawAsync("combined text", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult("combined text", 0.9f));

        var sut = CreateSut();
        var result = await sut.TranscribeRawAsync(new byte[] { 1 }, CancellationToken.None);

        Assert.Equal("combined text", result.Text);
        _transcriptionMock.Verify(x => x.TranscribeRawAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TranscribeRawAsync_StreamingActive_WithEmptyCombine_FallsBackToFullBuffer()
    {
        // Per the xmldoc fallback contract: empty streaming result means
        // "chunks were faulted or produced no text" — rerun Whisper on the
        // full buffer so dictation still produces something.
        _assemblerMock.SetupGet(x => x.IsActive).Returns(true);
        _assemblerMock.Setup(x => x.CombineAsync(It.IsAny<CancellationToken>())).ReturnsAsync("   ");
        _transcriptionMock
            .Setup(x => x.TranscribeRawAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult("rescued", 0.9f));

        var sut = CreateSut();
        var result = await sut.TranscribeRawAsync(new byte[] { 1 }, CancellationToken.None);

        Assert.Equal("rescued", result.Text);
        _transcriptionMock.Verify(x => x.FinalizePreTranscribedRawAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TranscribeFullAsync_ForwardsToTranscribeAsync()
    {
        _transcriptionMock
            .Setup(x => x.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult("full-pipeline", 0.9f));

        var sut = CreateSut();
        var result = await sut.TranscribeFullAsync(new byte[] { 1 }, CancellationToken.None);

        Assert.Equal("full-pipeline", result.Text);
        _transcriptionMock.Verify(x => x.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        _assemblerMock.Verify(x => x.CombineAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void RawTranscriptionReady_ForwardsFromUnderlyingService()
    {
        // The underlying ITranscriptionService raises this event mid-pipeline
        // so the UI can show "your words" before the LLM correction lands.
        // The transcriber must re-raise it so the worker keeps the same API
        // after the extraction.
        string? captured = null;
        var sut = CreateSut();
        sut.RawTranscriptionReady += text => captured = text;

        _transcriptionMock.Raise(x => x.RawTranscriptionReady += null, "hello");

        Assert.Equal("hello", captured);
    }
}
