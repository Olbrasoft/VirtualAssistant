using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Audio;
using Olbrasoft.VirtualAssistant.Voice.Audio;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Tests.Services;

/// <summary>
/// Unit tests for AudioRecordingCoordinator.
/// Tests audio recording workflow, buffer management, and error handling.
/// </summary>
public class AudioRecordingCoordinatorTests
{
    private readonly Mock<ILogger<AudioRecordingCoordinator>> _loggerMock;
    private readonly Mock<IAudioCaptureService> _audioCaptureMock;
    private readonly IOptions<AudioRecordingOptions> _defaultOptions;
    private readonly AudioRecordingCoordinator _sut;

    public AudioRecordingCoordinatorTests()
    {
        _loggerMock = new Mock<ILogger<AudioRecordingCoordinator>>();
        _audioCaptureMock = new Mock<IAudioCaptureService>();
        _defaultOptions = Options.Create(new AudioRecordingOptions
        {
            SampleRate = 16000,
            BitsPerSample = 16,
            Channels = 1,
            MaxRecordingDurationMinutes = 16
        });

        _sut = new AudioRecordingCoordinator(
            _loggerMock.Object,
            _audioCaptureMock.Object,
            _defaultOptions);
    }

    #region StartRecordingAsync Tests

    [Fact]
    public async Task StartRecordingAsync_StartsAudioCapture()
    {
        // Arrange
        _audioCaptureMock.Setup(x => x.ReadChunkAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null); // No chunks

        // Act
        await _sut.StartRecordingAsync();

        // Assert
        _audioCaptureMock.Verify(x => x.Start(), Times.Once);
        Assert.True(_sut.IsRecording);
    }

    [Fact]
    public async Task StartRecordingAsync_WhenAlreadyRecording_IgnoresRequest()
    {
        // Arrange
        _audioCaptureMock.Setup(x => x.ReadChunkAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        await _sut.StartRecordingAsync();

        // Act
        await _sut.StartRecordingAsync(); // Second call

        // Assert - Start should only be called once
        _audioCaptureMock.Verify(x => x.Start(), Times.Once);
    }

    [Fact]
    public async Task StartRecordingAsync_ClearsBufferBeforeStart()
    {
        // Arrange - Simulate recording some data then stopping and starting again
        var chunk1 = new byte[] { 1, 2, 3 };
        var callCount = 0;

        _audioCaptureMock.Setup(x => x.ReadChunkAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? chunk1 : null; // Return chunk on first call, then null
            });

        // First recording session
        await _sut.StartRecordingAsync();
        await Task.Delay(100); // Allow chunk to be captured
        var firstData = await _sut.StopRecordingAsync();

        // Act - Second recording session (buffer should be cleared)
        await _sut.StartRecordingAsync();
        var secondData = await _sut.StopRecordingAsync();

        // Assert - Second session should not contain data from first session
        Assert.NotEmpty(firstData);
        Assert.Empty(secondData); // No new chunks returned
    }

    #endregion

    #region StopRecordingAsync Tests

    [Fact]
    public async Task StopRecordingAsync_ReturnsEmptyArray_WhenNotRecording()
    {
        // Act
        var result = await _sut.StopRecordingAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task StopRecordingAsync_StopsAudioCaptureAndReturnsData()
    {
        // Arrange
        var chunk1 = new byte[] { 1, 2, 3 };
        var chunk2 = new byte[] { 4, 5, 6 };
        var callCount = 0;

        _audioCaptureMock.Setup(x => x.ReadChunkAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount switch
                {
                    1 => chunk1,
                    2 => chunk2,
                    _ => null // Stop after 2 chunks
                };
            });

        await _sut.StartRecordingAsync();
        await Task.Delay(200); // Allow chunks to be captured

        // Act
        var result = await _sut.StopRecordingAsync();

        // Assert
        _audioCaptureMock.Verify(x => x.Stop(), Times.Once);
        Assert.Equal(6, result.Length); // chunk1 + chunk2
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, result);
        Assert.False(_sut.IsRecording);
    }

    [Fact]
    public async Task StopRecordingAsync_ClearsBuffer()
    {
        // Arrange
        var chunk = new byte[] { 1, 2, 3 };
        var callCount = 0;
        _audioCaptureMock.Setup(x => x.ReadChunkAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount <= 1 ? chunk : null;
            });

        await _sut.StartRecordingAsync();
        await Task.Delay(100);

        // Act
        var firstStop = await _sut.StopRecordingAsync();
        await _sut.StartRecordingAsync();
        await Task.Delay(50); // Give time for loop to realize no more data
        var secondStop = await _sut.StopRecordingAsync();

        // Assert - Second stop should not contain data from first recording
        Assert.NotEmpty(firstStop);
        Assert.Empty(secondStop);
    }

    #endregion

    #region EmergencyStopAsync Tests

    [Fact]
    public async Task EmergencyStopAsync_StopsRecordingAndDiscardsData()
    {
        // Arrange
        var chunk = new byte[] { 1, 2, 3 };
        var callCount = 0;
        _audioCaptureMock.Setup(x => x.ReadChunkAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                // Return chunk for first recording, null for second recording (after emergency stop)
                callCount++;
                return callCount <= 2 ? chunk : null;
            });

        await _sut.StartRecordingAsync();
        await Task.Delay(100); // Allow chunk to be captured

        // Act
        await _sut.EmergencyStopAsync();

        // Assert
        _audioCaptureMock.Verify(x => x.Stop(), Times.Once);
        Assert.False(_sut.IsRecording);

        // Verify buffer was cleared (start new recording and stop immediately - should be empty)
        await _sut.StartRecordingAsync();
        var result = await _sut.StopRecordingAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task EmergencyStopAsync_WhenNotRecording_DoesNothing()
    {
        // Act
        await _sut.EmergencyStopAsync();

        // Assert
        _audioCaptureMock.Verify(x => x.Stop(), Times.Never);
        Assert.False(_sut.IsRecording);
    }

    #endregion

    #region IsRecording Property Tests

    [Fact]
    public void IsRecording_InitiallyFalse()
    {
        // Assert
        Assert.False(_sut.IsRecording);
    }

    [Fact]
    public async Task IsRecording_TrueAfterStart_FalseAfterStop()
    {
        // Arrange
        _audioCaptureMock.Setup(x => x.ReadChunkAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // Act & Assert
        Assert.False(_sut.IsRecording);

        await _sut.StartRecordingAsync();
        Assert.True(_sut.IsRecording);

        await _sut.StopRecordingAsync();
        Assert.False(_sut.IsRecording);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task StartRecordingAsync_WhenAudioCaptureStartFails_ThrowsException()
    {
        // Arrange
        _audioCaptureMock.Setup(x => x.Start())
            .Throws(new InvalidOperationException("Audio device error"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.StartRecordingAsync());

        Assert.False(_sut.IsRecording);
    }

    [Fact]
    public async Task StopRecordingAsync_WhenAudioCaptureStopFails_ThrowsException()
    {
        // Arrange
        var chunk = new byte[] { 1, 2, 3 };
        _audioCaptureMock.Setup(x => x.ReadChunkAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunk);
        _audioCaptureMock.Setup(x => x.Stop())
            .Throws(new InvalidOperationException("Stop failed"));

        await _sut.StartRecordingAsync();
        await Task.Delay(100); // TODO: Replace timing-dependent delay with deterministic synchronization

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.StopRecordingAsync());

        Assert.Equal("Stop failed", exception.Message);
    }

    #endregion
}
