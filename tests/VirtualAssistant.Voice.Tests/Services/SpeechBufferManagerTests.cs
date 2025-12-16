using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace VirtualAssistant.Voice.Tests.Services;

public class SpeechBufferManagerTests
{
    private readonly Mock<ILogger<SpeechBufferManager>> _loggerMock;
    private readonly IOptions<ContinuousListenerOptions> _options;
    private readonly SpeechBufferManager _manager;

    public SpeechBufferManagerTests()
    {
        _loggerMock = new Mock<ILogger<SpeechBufferManager>>();
        // PreBufferMaxBytes = SampleRate * PreBufferMs / 1000 * 2
        // 1000 * 500 / 1000 * 2 = 1000 bytes
        _options = Options.Create(new ContinuousListenerOptions
        {
            SampleRate = 1000,
            PreBufferMs = 500
        });
        _manager = new SpeechBufferManager(_loggerMock.Object, _options);
    }

    #region AddToPreBuffer Tests

    [Fact]
    public void AddToPreBuffer_AddsChunk()
    {
        var chunk = new byte[] { 1, 2, 3, 4, 5 };

        _manager.AddToPreBuffer(chunk);

        // Transfer to speech buffer to verify
        _manager.TransferPreBufferToSpeech();
        var result = _manager.GetCombinedSpeechData();

        Assert.Equal(5, result.Length);
        Assert.Equal(chunk, result);
    }

    [Fact]
    public void AddToPreBuffer_TrimsWhenExceedsMax()
    {
        // Add 600 bytes, then 600 more (total 1200 > 1000 max)
        var chunk1 = new byte[600];
        var chunk2 = new byte[600];

        _manager.AddToPreBuffer(chunk1);
        _manager.AddToPreBuffer(chunk2);

        // Transfer and verify - first chunk should be trimmed
        _manager.TransferPreBufferToSpeech();
        Assert.Equal(600, _manager.SpeechBufferSize);
    }

    #endregion

    #region TransferPreBufferToSpeech Tests

    [Fact]
    public void TransferPreBufferToSpeech_MovesAllChunks()
    {
        _manager.AddToPreBuffer(new byte[] { 1, 2, 3 });
        _manager.AddToPreBuffer(new byte[] { 4, 5, 6 });

        _manager.TransferPreBufferToSpeech();

        Assert.Equal(6, _manager.SpeechBufferSize);
    }

    [Fact]
    public void TransferPreBufferToSpeech_ClearsPreBuffer()
    {
        _manager.AddToPreBuffer(new byte[] { 1, 2, 3 });
        _manager.TransferPreBufferToSpeech();

        // Add more to pre-buffer after transfer
        _manager.AddToPreBuffer(new byte[] { 7, 8, 9 });
        _manager.ClearSpeechBuffer();
        _manager.TransferPreBufferToSpeech();

        // Only the new chunk should be in speech buffer
        Assert.Equal(3, _manager.SpeechBufferSize);
    }

    #endregion

    #region AddToSpeechBuffer Tests

    [Fact]
    public void AddToSpeechBuffer_AddsDirectly()
    {
        _manager.AddToSpeechBuffer(new byte[] { 1, 2, 3 });
        _manager.AddToSpeechBuffer(new byte[] { 4, 5 });

        Assert.Equal(5, _manager.SpeechBufferSize);
    }

    #endregion

    #region GetCombinedSpeechData Tests

    [Fact]
    public void GetCombinedSpeechData_CombinesAllChunks()
    {
        _manager.AddToSpeechBuffer(new byte[] { 1, 2, 3 });
        _manager.AddToSpeechBuffer(new byte[] { 4, 5, 6 });

        var result = _manager.GetCombinedSpeechData();

        Assert.Equal(6, result.Length);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, result);
    }

    [Fact]
    public void GetCombinedSpeechData_ReturnsEmptyArrayWhenEmpty()
    {
        var result = _manager.GetCombinedSpeechData();

        Assert.Empty(result);
    }

    #endregion

    #region ClearSpeechBuffer Tests

    [Fact]
    public void ClearSpeechBuffer_ClearsOnlySpeechBuffer()
    {
        _manager.AddToPreBuffer(new byte[] { 1, 2, 3 });
        _manager.AddToSpeechBuffer(new byte[] { 4, 5, 6 });

        _manager.ClearSpeechBuffer();

        Assert.Equal(0, _manager.SpeechBufferSize);

        // Pre-buffer should still have data
        _manager.TransferPreBufferToSpeech();
        Assert.Equal(3, _manager.SpeechBufferSize);
    }

    #endregion

    #region ClearAll Tests

    [Fact]
    public void ClearAll_ClearsBothBuffers()
    {
        _manager.AddToPreBuffer(new byte[] { 1, 2, 3 });
        _manager.AddToSpeechBuffer(new byte[] { 4, 5, 6 });

        _manager.ClearAll();

        Assert.Equal(0, _manager.SpeechBufferSize);

        // Pre-buffer should also be empty
        _manager.TransferPreBufferToSpeech();
        Assert.Equal(0, _manager.SpeechBufferSize);
    }

    #endregion
}
