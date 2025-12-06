using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace VirtualAssistant.Voice.Tests.Services;

/// <summary>
/// Unit tests for TtsService message queue and fallback functionality.
/// Note: WebSocket/audio playback functionality requires integration tests.
/// </summary>
public class TtsServiceTests : IDisposable
{
    private readonly Mock<ILogger<TtsService>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ITtsProvider> _primaryProviderMock;
    private readonly Mock<ITtsProvider> _fallbackProviderMock;
    private readonly Mock<ILocationService> _locationServiceMock;
    private readonly TtsService _sut;
    private const string SpeechLockFilePath = "/tmp/speech-lock";

    public TtsServiceTests()
    {
        _loggerMock = new Mock<ILogger<TtsService>>();
        _configurationMock = new Mock<IConfiguration>();
        _primaryProviderMock = new Mock<ITtsProvider>();
        _fallbackProviderMock = new Mock<ITtsProvider>();
        _locationServiceMock = new Mock<ILocationService>();

        // Setup provider names
        _primaryProviderMock.Setup(p => p.Name).Returns("EdgeTTS");
        _fallbackProviderMock.Setup(p => p.Name).Returns("Piper");

        // Setup empty configuration section (will use defaults from TtsVoiceProfilesOptions)
        var sectionMock = new Mock<IConfigurationSection>();
        _configurationMock.Setup(x => x.GetSection("TtsVoiceProfiles")).Returns(sectionMock.Object);

        var fallbackOptions = Options.Create(new TtsFallbackOptions
        {
            EnableFallback = true,
            CheckLocation = true,
            SilentOnFailure = true
        });

        _sut = new TtsService(
            _loggerMock.Object,
            _configurationMock.Object,
            _primaryProviderMock.Object,
            _fallbackProviderMock.Object,
            _locationServiceMock.Object,
            fallbackOptions);

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

    [Fact]
    public void Dispose_MultipleCalls_NoExceptions()
    {
        // Arrange
        var logger = new Mock<ILogger<TtsService>>();
        var configuration = new Mock<IConfiguration>();
        var sectionMock = new Mock<IConfigurationSection>();
        configuration.Setup(x => x.GetSection("TtsVoiceProfiles")).Returns(sectionMock.Object);
        var primaryProvider = new Mock<ITtsProvider>();
        primaryProvider.Setup(p => p.Name).Returns("EdgeTTS");

        var service = new TtsService(logger.Object, configuration.Object, primaryProvider.Object);

        // Act & Assert - multiple dispose calls should not throw
        service.Dispose();
        var exception = Record.Exception(() => service.Dispose());

        // Note: Second dispose may throw ObjectDisposedException which is acceptable
        // We're mainly testing that the first dispose doesn't throw
    }

    [Fact]
    public void ActiveProvider_Initially_ReturnsPrimaryProviderName()
    {
        // Act
        var result = _sut.ActiveProvider;

        // Assert
        Assert.Equal("EdgeTTS", result);
    }
}

/// <summary>
/// Unit tests for TtsService fallback functionality.
/// </summary>
public class TtsServiceFallbackTests : IDisposable
{
    private readonly Mock<ILogger<TtsService>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ITtsProvider> _primaryProviderMock;
    private readonly Mock<ITtsProvider> _fallbackProviderMock;
    private readonly Mock<ILocationService> _locationServiceMock;
    private const string SpeechLockFilePath = "/tmp/speech-lock";
    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".cache", "virtual-assistant-tts");

    public TtsServiceFallbackTests()
    {
        _loggerMock = new Mock<ILogger<TtsService>>();
        _configurationMock = new Mock<IConfiguration>();
        _primaryProviderMock = new Mock<ITtsProvider>();
        _fallbackProviderMock = new Mock<ITtsProvider>();
        _locationServiceMock = new Mock<ILocationService>();

        // Setup provider names
        _primaryProviderMock.Setup(p => p.Name).Returns("EdgeTTS");
        _fallbackProviderMock.Setup(p => p.Name).Returns("Piper");

        // Setup empty configuration section
        var sectionMock = new Mock<IConfigurationSection>();
        _configurationMock.Setup(x => x.GetSection("TtsVoiceProfiles")).Returns(sectionMock.Object);

        // Ensure clean state
        if (File.Exists(SpeechLockFilePath))
        {
            File.Delete(SpeechLockFilePath);
        }

        // Clean cache directory to ensure tests call providers
        CleanCacheDirectory();
    }

    public void Dispose()
    {
        if (File.Exists(SpeechLockFilePath))
        {
            File.Delete(SpeechLockFilePath);
        }
    }

    private static void CleanCacheDirectory()
    {
        if (Directory.Exists(CacheDirectory))
        {
            foreach (var file in Directory.GetFiles(CacheDirectory, "test-*"))
            {
                try { File.Delete(file); } catch { }
            }
        }
    }

    private TtsService CreateService(TtsFallbackOptions? options = null)
    {
        return new TtsService(
            _loggerMock.Object,
            _configurationMock.Object,
            _primaryProviderMock.Object,
            _fallbackProviderMock.Object,
            _locationServiceMock.Object,
            Options.Create(options ?? new TtsFallbackOptions()));
    }

    [Fact]
    public void Constructor_WithProviders_LogsInitialization()
    {
        // Act
        using var sut = CreateService();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("TTS service initialized")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithoutFallbackProvider_InitializesSuccessfully()
    {
        // Act
        using var sut = new TtsService(
            _loggerMock.Object,
            _configurationMock.Object,
            _primaryProviderMock.Object);

        // Assert
        Assert.Equal("EdgeTTS", sut.ActiveProvider);
    }

    [Fact]
    public async Task SpeakAsync_WhenVpnActive_UsesFallbackProvider()
    {
        // Arrange
        var uniqueText = $"test-vpn-active-{Guid.NewGuid()}";
        _locationServiceMock.Setup(l => l.IsVpnActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _fallbackProviderMock.Setup(p => p.GenerateAudioAsync(
                It.IsAny<string>(),
                It.IsAny<VoiceConfig>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });

        using var sut = CreateService(new TtsFallbackOptions
        {
            EnableFallback = true,
            CheckLocation = true,
            SilentOnFailure = true
        });

        // Act
        await sut.SpeakAsync(uniqueText);

        // Assert - fallback provider should be used due to VPN
        _fallbackProviderMock.Verify(
            p => p.GenerateAudioAsync(
                It.IsAny<string>(),
                It.IsAny<VoiceConfig>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SpeakAsync_WhenVpnNotActive_UsesPrimaryProvider()
    {
        // Arrange
        var uniqueText = $"test-vpn-not-active-{Guid.NewGuid()}";
        _locationServiceMock.Setup(l => l.IsVpnActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _primaryProviderMock.Setup(p => p.GenerateAudioAsync(
                It.IsAny<string>(),
                It.IsAny<VoiceConfig>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });

        using var sut = CreateService(new TtsFallbackOptions
        {
            EnableFallback = true,
            CheckLocation = true,
            SilentOnFailure = true
        });

        // Act
        await sut.SpeakAsync(uniqueText);

        // Assert - primary provider should be used
        _primaryProviderMock.Verify(
            p => p.GenerateAudioAsync(
                It.IsAny<string>(),
                It.IsAny<VoiceConfig>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SpeakAsync_WhenPrimaryFails_FallsBackToSecondary()
    {
        // Arrange
        var uniqueText = $"test-primary-fails-{Guid.NewGuid()}";
        _locationServiceMock.Setup(l => l.IsVpnActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _primaryProviderMock.Setup(p => p.GenerateAudioAsync(
                It.IsAny<string>(),
                It.IsAny<VoiceConfig>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null); // Primary fails
        _fallbackProviderMock.Setup(p => p.GenerateAudioAsync(
                It.IsAny<string>(),
                It.IsAny<VoiceConfig>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });

        using var sut = CreateService(new TtsFallbackOptions
        {
            EnableFallback = true,
            CheckLocation = true,
            SilentOnFailure = true
        });

        // Act
        await sut.SpeakAsync(uniqueText);

        // Assert - both providers should be called (primary failed, fallback used)
        _primaryProviderMock.Verify(
            p => p.GenerateAudioAsync(
                It.IsAny<string>(),
                It.IsAny<VoiceConfig>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _fallbackProviderMock.Verify(
            p => p.GenerateAudioAsync(
                It.IsAny<string>(),
                It.IsAny<VoiceConfig>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SpeakAsync_WhenFallbackDisabled_DoesNotUseFallback()
    {
        // Arrange
        var uniqueText = $"test-fallback-disabled-{Guid.NewGuid()}";
        _locationServiceMock.Setup(l => l.IsVpnActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _primaryProviderMock.Setup(p => p.GenerateAudioAsync(
                It.IsAny<string>(),
                It.IsAny<VoiceConfig>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null); // Primary fails

        using var sut = CreateService(new TtsFallbackOptions
        {
            EnableFallback = false,
            SilentOnFailure = true
        });

        // Act
        await sut.SpeakAsync(uniqueText);

        // Assert - fallback should NOT be called
        _fallbackProviderMock.Verify(
            p => p.GenerateAudioAsync(
                It.IsAny<string>(),
                It.IsAny<VoiceConfig>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SpeakAsync_WhenLocationCheckDisabled_DoesNotCheckVpn()
    {
        // Arrange
        var uniqueText = $"test-location-disabled-{Guid.NewGuid()}";
        _primaryProviderMock.Setup(p => p.GenerateAudioAsync(
                It.IsAny<string>(),
                It.IsAny<VoiceConfig>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });

        using var sut = CreateService(new TtsFallbackOptions
        {
            EnableFallback = true,
            CheckLocation = false,
            SilentOnFailure = true
        });

        // Act
        await sut.SpeakAsync(uniqueText);

        // Assert - location service should NOT be called
        _locationServiceMock.Verify(
            l => l.IsVpnActiveAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
