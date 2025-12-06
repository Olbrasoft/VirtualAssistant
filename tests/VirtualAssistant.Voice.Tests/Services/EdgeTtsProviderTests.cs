using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace VirtualAssistant.Voice.Tests.Services;

/// <summary>
/// Unit tests for EdgeTtsProvider.
/// Note: GenerateAudioAsync requires integration tests due to WebSocket dependency.
/// </summary>
public class EdgeTtsProviderTests
{
    private readonly Mock<ILogger<EdgeTtsProvider>> _loggerMock;
    private readonly EdgeTtsProvider _sut;

    public EdgeTtsProviderTests()
    {
        _loggerMock = new Mock<ILogger<EdgeTtsProvider>>();
        _sut = new EdgeTtsProvider(_loggerMock.Object);
    }

    [Fact]
    public void Name_ReturnsEdgeTTS()
    {
        // Act
        var result = _sut.Name;

        // Assert
        Assert.Equal("EdgeTTS", result);
    }

    [Fact]
    public void IsAvailable_ReturnsTrue()
    {
        // Act
        var result = _sut.IsAvailable;

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Constructor_WithValidLogger_CreatesInstance()
    {
        // Arrange & Act
        var provider = new EdgeTtsProvider(_loggerMock.Object);

        // Assert
        Assert.NotNull(provider);
        Assert.Equal("EdgeTTS", provider.Name);
    }

    [Fact]
    public void ITtsProvider_Interface_IsImplemented()
    {
        // Assert
        Assert.IsAssignableFrom<ITtsProvider>(_sut);
    }

    [Fact]
    public async Task GenerateAudioAsync_WithCancellation_ReturnsNull()
    {
        // Arrange
        var config = new VoiceConfig("cs-CZ-AntoninNeural", "+0%", "+0%", "+0Hz");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act - WebSocket errors are caught and return null
        var result = await _sut.GenerateAudioAsync("Test", config, cts.Token);

        // Assert - returns null when cancelled/failed
        Assert.Null(result);
    }

    // Note: Integration tests for actual audio generation would require
    // mocking WebSocket or using a test server, which is beyond unit testing scope.
    // The WebSocket functionality should be tested via integration tests.
}
