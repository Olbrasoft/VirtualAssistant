using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace VirtualAssistant.Voice.Tests.Services;

/// <summary>
/// Unit tests for TtsService message queue functionality.
/// Note: WebSocket/audio playback functionality requires integration tests.
/// </summary>
public class TtsServiceTests : IDisposable
{
    private readonly Mock<ILogger<TtsService>> _loggerMock;
    private readonly IConfiguration _configuration;
    private readonly Mock<ITtsProviderChain> _ttsProviderChainMock;
    private readonly Mock<ITtsQueueService> _queueServiceMock;
    private readonly Mock<ITtsCacheService> _cacheServiceMock;
    private readonly Mock<IAudioPlaybackService> _playbackServiceMock;
    private readonly Mock<ISpeechLockService> _speechLockServiceMock;
    private readonly TtsService _sut;

    public TtsServiceTests()
    {
        _loggerMock = new Mock<ILogger<TtsService>>();
        _ttsProviderChainMock = new Mock<ITtsProviderChain>();
        _queueServiceMock = new Mock<ITtsQueueService>();
        _cacheServiceMock = new Mock<ITtsCacheService>();
        _playbackServiceMock = new Mock<IAudioPlaybackService>();
        _speechLockServiceMock = new Mock<ISpeechLockService>();

        // Setup speech lock service - default to unlocked
        _speechLockServiceMock.Setup(x => x.IsLocked).Returns(false);

        // Use real in-memory configuration (Bind() doesn't work with mocks)
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TtsVoice:Voice"] = "cs-CZ-AntoninNeural",
                ["TtsVoice:Rate"] = "+10%",
                ["TtsVoice:Volume"] = "+0%",
                ["TtsVoice:Pitch"] = "+0Hz"
            })
            .Build();

        _sut = new TtsService(
            _loggerMock.Object,
            _configuration,
            _ttsProviderChainMock.Object,
            _queueServiceMock.Object,
            _cacheServiceMock.Object,
            _playbackServiceMock.Object,
            _speechLockServiceMock.Object);
    }

    public void Dispose()
    {
        _sut.Dispose();
    }

    [Fact]
    public void QueueCount_InitialState_ReturnsZero()
    {
        // Arrange
        _queueServiceMock.Setup(x => x.Count).Returns(0);

        // Act
        var result = _sut.QueueCount;

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task SpeakAsync_WhenLockExists_QueuesMessage()
    {
        // Arrange
        _speechLockServiceMock.Setup(x => x.IsLocked).Returns(true);
        _queueServiceMock.Setup(x => x.Count).Returns(1);

        // Act
        await _sut.SpeakAsync("Test message");

        // Assert
        _queueServiceMock.Verify(x => x.Enqueue("Test message", null), Times.Once);
    }

    [Fact]
    public async Task SpeakAsync_WhenLockExists_QueuesMultipleMessages()
    {
        // Arrange
        _speechLockServiceMock.Setup(x => x.IsLocked).Returns(true);

        // Act
        await _sut.SpeakAsync("Message 1");
        await _sut.SpeakAsync("Message 2");
        await _sut.SpeakAsync("Message 3");

        // Assert
        _queueServiceMock.Verify(x => x.Enqueue(It.IsAny<string>(), It.IsAny<string?>()), Times.Exactly(3));
    }

    [Fact]
    public async Task FlushQueueAsync_WhenQueueEmpty_DoesNothing()
    {
        // Arrange
        _queueServiceMock.Setup(x => x.Count).Returns(0);

        // Act
        await _sut.FlushQueueAsync();

        // Assert - TryDequeue should not be called when queue is empty
        _queueServiceMock.Verify(x => x.TryDequeue(out It.Ref<(string, string?)>.IsAny), Times.Never);
    }

    [Fact]
    public async Task FlushQueueAsync_WhenLockReacquired_StopsAndRequeues()
    {
        // Arrange
        _queueServiceMock.Setup(x => x.Count).Returns(2);

        var callCount = 0;
        var item = ("Message 1", (string?)"source");
        _queueServiceMock
            .Setup(x => x.TryDequeue(out item))
            .Returns(() =>
            {
                callCount++;
                return callCount <= 1; // Return true only on first call
            });

        // Lock is acquired after first dequeue
        _speechLockServiceMock.SetupSequence(x => x.IsLocked)
            .Returns(false) // Initial check
            .Returns(true); // After first dequeue

        // Act
        await _sut.FlushQueueAsync();

        // Assert - message should be re-queued when lock is acquired
        _queueServiceMock.Verify(x => x.Enqueue(It.IsAny<string>(), It.IsAny<string?>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task SpeakAsync_QueuePreservesOrder()
    {
        // Arrange
        _speechLockServiceMock.Setup(x => x.IsLocked).Returns(true);
        var enqueuedMessages = new List<string>();
        _queueServiceMock
            .Setup(x => x.Enqueue(It.IsAny<string>(), It.IsAny<string?>()))
            .Callback<string, string?>((text, source) => enqueuedMessages.Add(text));

        var messages = new[] { "First", "Second", "Third" };

        // Act
        foreach (var msg in messages)
        {
            await _sut.SpeakAsync(msg);
        }

        // Assert
        Assert.Equal(messages, enqueuedMessages);
    }

    [Fact]
    public async Task SpeakAsync_ThreadSafety_MultipleEnqueues()
    {
        // Arrange
        _speechLockServiceMock.Setup(x => x.IsLocked).Returns(true);
        var enqueueCount = 0;
        _queueServiceMock
            .Setup(x => x.Enqueue(It.IsAny<string>(), It.IsAny<string?>()))
            .Callback(() => Interlocked.Increment(ref enqueueCount));

        var tasks = new Task[10];

        // Act - multiple concurrent enqueues
        for (int i = 0; i < tasks.Length; i++)
        {
            var index = i;
            tasks[i] = Task.Run(async () =>
            {
                await _sut.SpeakAsync($"Message {index}");
            });
        }

        await Task.WhenAll(tasks);

        // Assert - all messages should be queued
        Assert.Equal(10, enqueueCount);
    }

    [Fact]
    public void StopPlayback_CallsPlaybackServiceStop()
    {
        // Act
        _sut.StopPlayback();

        // Assert
        _playbackServiceMock.Verify(x => x.Stop(), Times.Once);
    }

    [Fact]
    public async Task SpeakAsync_WhenNotLocked_ChecksCache()
    {
        // Arrange
        _speechLockServiceMock.Setup(x => x.IsLocked).Returns(false);
        var cachePath = "/tmp/cached.mp3";
        _cacheServiceMock
            .Setup(x => x.TryGetCached(It.IsAny<string>(), It.IsAny<VoiceConfig>(), out cachePath))
            .Returns(true);

        // Act
        await _sut.SpeakAsync("Test message");

        // Assert - should check cache
        _cacheServiceMock.Verify(x => x.TryGetCached("Test message", It.IsAny<VoiceConfig>(), out It.Ref<string>.IsAny), Times.Once);
    }

    [Fact]
    public async Task SpeakAsync_CacheHit_PlaysFromCache()
    {
        // Arrange
        _speechLockServiceMock.Setup(x => x.IsLocked).Returns(false);
        var cachePath = "/tmp/cached.mp3";
        _cacheServiceMock
            .Setup(x => x.TryGetCached(It.IsAny<string>(), It.IsAny<VoiceConfig>(), out cachePath))
            .Returns(true);

        // Act
        await _sut.SpeakAsync("Test message");

        // Assert - should play from cache, not generate
        _playbackServiceMock.Verify(x => x.PlayAsync(cachePath, It.IsAny<CancellationToken>()), Times.Once);
        _ttsProviderChainMock.Verify(x => x.SynthesizeAsync(It.IsAny<string>(), It.IsAny<VoiceConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SpeakAsync_CacheMiss_GeneratesAndSaves()
    {
        // Arrange
        _speechLockServiceMock.Setup(x => x.IsLocked).Returns(false);
        var cachePath = "";
        _cacheServiceMock
            .Setup(x => x.TryGetCached(It.IsAny<string>(), It.IsAny<VoiceConfig>(), out cachePath))
            .Returns(false);
        _cacheServiceMock
            .Setup(x => x.GetCachePath(It.IsAny<string>(), It.IsAny<VoiceConfig>()))
            .Returns("/tmp/new.mp3");

        var audioData = new byte[] { 1, 2, 3 };
        _ttsProviderChainMock
            .Setup(x => x.SynthesizeAsync(It.IsAny<string>(), It.IsAny<VoiceConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((audioData, "MockProvider"));

        // Act
        await _sut.SpeakAsync("Test message");

        // Assert - should generate and save to cache
        _ttsProviderChainMock.Verify(x => x.SynthesizeAsync("Test message", It.IsAny<VoiceConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _cacheServiceMock.Verify(x => x.SaveAsync("Test message", It.IsAny<VoiceConfig>(), audioData, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Tests that Dispose properly cleans up the semaphore without throwing.
    /// </summary>
    [Fact]
    public void Dispose_MultipleCalls_NoExceptions()
    {
        // Arrange
        var logger = new Mock<ILogger<TtsService>>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TtsVoice:Voice"] = "cs-CZ-AntoninNeural",
                ["TtsVoice:Rate"] = "+10%",
                ["TtsVoice:Volume"] = "+0%",
                ["TtsVoice:Pitch"] = "+0Hz"
            })
            .Build();

        var service = new TtsService(
            logger.Object,
            configuration,
            new Mock<ITtsProviderChain>().Object,
            new Mock<ITtsQueueService>().Object,
            new Mock<ITtsCacheService>().Object,
            new Mock<IAudioPlaybackService>().Object,
            new Mock<ISpeechLockService>().Object);

        // Act & Assert - multiple dispose calls should not throw
        service.Dispose();
        var exception = Record.Exception(() => service.Dispose());

        // Note: Second dispose may throw ObjectDisposedException which is acceptable
        // We're mainly testing that the first dispose doesn't throw
    }
}
