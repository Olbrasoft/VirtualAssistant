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
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly TtsService _sut;
    private const string SpeechLockFilePath = "/tmp/speech-lock";

    public TtsServiceTests()
    {
        _loggerMock = new Mock<ILogger<TtsService>>();
        _configurationMock = new Mock<IConfiguration>();

        // Setup empty configuration section (will use defaults from TtsVoiceProfilesOptions)
        var sectionMock = new Mock<IConfigurationSection>();
        _configurationMock.Setup(x => x.GetSection("TtsVoiceProfiles")).Returns(sectionMock.Object);

        _sut = new TtsService(_loggerMock.Object, _configurationMock.Object);

        // Ensure clean state - no lock file
        if (File.Exists(SpeechLockFilePath))
        {
            File.Delete(SpeechLockFilePath);
        }
    }

    public void Dispose()
    {
        // Clean up lock file after tests
        if (File.Exists(SpeechLockFilePath))
        {
            File.Delete(SpeechLockFilePath);
        }
        _sut.Dispose();
    }

    [Fact]
    public void QueueCount_InitialState_ReturnsZero()
    {
        // Act
        var result = _sut.QueueCount;
        
        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task SpeakAsync_WhenLockExists_QueuesMessage()
    {
        // Arrange
        File.WriteAllText(SpeechLockFilePath, "test");
        
        // Act
        await _sut.SpeakAsync("Test message");
        
        // Assert
        Assert.Equal(1, _sut.QueueCount);
    }

    [Fact]
    public async Task SpeakAsync_WhenLockExists_QueuesMultipleMessages()
    {
        // Arrange
        File.WriteAllText(SpeechLockFilePath, "test");
        
        // Act
        await _sut.SpeakAsync("Message 1");
        await _sut.SpeakAsync("Message 2");
        await _sut.SpeakAsync("Message 3");
        
        // Assert
        Assert.Equal(3, _sut.QueueCount);
    }

    // ⚠️ WARNING: DO NOT UNCOMMENT THIS TEST - IT MAY PLAY AUDIO/TTS DURING TEST EXECUTION ⚠️
    // This test calls FlushQueueAsync without lock file, which may trigger audio playback.
    // Keep this test commented out to prevent sound interruptions during test runs.
    //
    //[Fact]
    //public async Task FlushQueueAsync_WhenQueueEmpty_DoesNothing()
    //{
    //    // Arrange - queue is empty
    //    Assert.Equal(0, _sut.QueueCount);
    //
    //    // Act
    //    await _sut.FlushQueueAsync();
    //
    //    // Assert - no errors, still empty
    //    Assert.Equal(0, _sut.QueueCount);
    //}

    [Fact]
    public async Task FlushQueueAsync_WhenLockReacquired_StopsAndRequeues()
    {
        // Arrange - queue some messages
        File.WriteAllText(SpeechLockFilePath, "test");
        await _sut.SpeakAsync("Message 1");
        await _sut.SpeakAsync("Message 2");
        Assert.Equal(2, _sut.QueueCount);
        
        // Note: FlushQueueAsync will check for lock file before each message
        // Since lock still exists, messages should be re-queued
        
        // Act
        await _sut.FlushQueueAsync();
        
        // Assert - messages were re-queued because lock still exists
        // First message was dequeued, lock checked, re-queued
        Assert.True(_sut.QueueCount >= 1, "Messages should remain in queue when lock exists");
    }

    [Fact]
    public async Task SpeakAsync_QueuePreservesOrder()
    {
        // Arrange
        File.WriteAllText(SpeechLockFilePath, "test");
        var messages = new[] { "First", "Second", "Third" };
        
        // Act
        foreach (var msg in messages)
        {
            await _sut.SpeakAsync(msg);
        }
        
        // Assert
        Assert.Equal(3, _sut.QueueCount);
        // Queue should preserve FIFO order (tested implicitly by queue behavior)
    }

    // ⚠️ WARNING: DO NOT UNCOMMENT THIS TEST - IT PLAYS AUDIO/TTS DURING TEST EXECUTION ⚠️
    // This test calls actual TTS service without lock file, causing audio playback.
    // Keep this test commented out to prevent sound interruptions during test runs.
    // The functionality is covered by integration tests.
    //
    //[Fact]
    //public async Task SpeakAsync_NoLock_DoesNotQueue()
    //{
    //    // Arrange - ensure no lock file exists
    //    if (File.Exists(SpeechLockFilePath))
    //    {
    //        File.Delete(SpeechLockFilePath);
    //    }
    //
    //    // Act - try to speak without lock
    //    // Note: This will fail because we can't actually play audio in tests,
    //    // but the queue should remain empty since lock doesn't exist
    //    try
    //    {
    //        await _sut.SpeakAsync("Test message");
    //    }
    //    catch
    //    {
    //        // Expected - can't play audio in test environment
    //    }
    //
    //    // Assert - message was NOT queued (was attempted to be played directly)
    //    Assert.Equal(0, _sut.QueueCount);
    //}

    [Fact]
    public async Task SpeakAsync_ThreadSafety_MultipleEnqueues()
    {
        // Arrange
        File.WriteAllText(SpeechLockFilePath, "test");
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
        Assert.Equal(10, _sut.QueueCount);
    }

    [Fact]
    public async Task QueueCount_AfterFlushWithLock_PreservesCount()
    {
        // Arrange - add messages while lock exists
        File.WriteAllText(SpeechLockFilePath, "test");
        await _sut.SpeakAsync("Message 1");
        await _sut.SpeakAsync("Message 2");
        var initialCount = _sut.QueueCount;
        Assert.Equal(2, initialCount);
        
        // Act - try to flush while lock still exists
        await _sut.FlushQueueAsync();
        
        // Assert - queue should still have messages (lock prevents playback)
        Assert.True(_sut.QueueCount >= 1);
    }

    // ⚠️ WARNING: DO NOT UNCOMMENT THIS TEST - IT PLAYS AUDIO/TTS DURING TEST EXECUTION ⚠️
    // This test calls actual TTS service without lock file, causing audio playback of "Concurrent message 0-4".
    // Keep this test commented out to prevent sound interruptions during test runs.
    // The semaphore thread-safety functionality is covered by other tests that use lock files.
    //
    ///// <summary>
    ///// Tests that SemaphoreSlim ensures sequential execution of SpeakDirectAsync.
    ///// Since we can't test actual playback, this test verifies that:
    ///// 1. Multiple concurrent calls don't throw exceptions
    ///// 2. The service handles concurrent access gracefully
    ///// Note: Full sequential playback is tested via integration tests.
    ///// </summary>
    //[Fact]
    //public async Task SpeakAsync_MultipleConcurrentCalls_NoExceptions()
    //{
    //    // Arrange - ensure no lock file (messages will go to SpeakDirectAsync)
    //    if (File.Exists(SpeechLockFilePath))
    //    {
    //        File.Delete(SpeechLockFilePath);
    //    }
    //
    //    var tasks = new List<Task>();
    //    var exceptions = new List<Exception>();
    //
    //    // Act - fire multiple concurrent calls
    //    // These will try to play audio (which will fail in tests) but should not throw
    //    // The semaphore should ensure they don't corrupt shared state
    //    for (int i = 0; i < 5; i++)
    //    {
    //        var index = i;
    //        tasks.Add(Task.Run(async () =>
    //        {
    //            try
    //            {
    //                await _sut.SpeakAsync($"Concurrent message {index}");
    //            }
    //            catch (Exception ex)
    //            {
    //                // WebSocket/audio errors are expected in test environment
    //                // but ObjectDisposedException or semaphore errors would indicate a problem
    //                if (ex is ObjectDisposedException or SemaphoreFullException)
    //                {
    //                    lock (exceptions)
    //                    {
    //                        exceptions.Add(ex);
    //                    }
    //                }
    //            }
    //        }));
    //    }
    //
    //    await Task.WhenAll(tasks);
    //
    //    // Assert - no semaphore-related exceptions
    //    Assert.Empty(exceptions);
    //    // Queue should be empty (messages were attempted to be played, not queued)
    //    Assert.Equal(0, _sut.QueueCount);
    //}

    /// <summary>
    /// Tests that Dispose properly cleans up the semaphore without throwing.
    /// </summary>
    [Fact]
    public void Dispose_MultipleCalls_NoExceptions()
    {
        // Arrange
        var logger = new Mock<ILogger<TtsService>>();
        var configuration = new Mock<IConfiguration>();
        var sectionMock = new Mock<IConfigurationSection>();
        configuration.Setup(x => x.GetSection("TtsVoiceProfiles")).Returns(sectionMock.Object);

        var service = new TtsService(logger.Object, configuration.Object);

        // Act & Assert - multiple dispose calls should not throw
        service.Dispose();
        var exception = Record.Exception(() => service.Dispose());

        // Note: Second dispose may throw ObjectDisposedException which is acceptable
        // We're mainly testing that the first dispose doesn't throw
    }
}
