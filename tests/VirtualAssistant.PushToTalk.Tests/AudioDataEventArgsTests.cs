using Olbrasoft.VirtualAssistant.PushToTalk;

namespace VirtualAssistant.PushToTalk.Tests;

public class AudioDataEventArgsTests
{
    [Fact]
    public void Constructor_ValidParameters_SetsAllProperties()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var timestamp = new DateTime(2025, 1, 1, 12, 0, 0);

        // Act
        var args = new AudioDataEventArgs(data, timestamp);

        // Assert
        Assert.Equal(data, args.Data);
        Assert.Equal(timestamp, args.Timestamp);
    }

    [Fact]
    public void Constructor_EmptyData_SetsEmptyArray()
    {
        // Arrange
        var data = Array.Empty<byte>();
        var timestamp = DateTime.UtcNow;

        // Act
        var args = new AudioDataEventArgs(data, timestamp);

        // Assert
        Assert.Empty(args.Data);
    }

    [Fact]
    public void Constructor_LargeData_PreservesAllBytes()
    {
        // Arrange
        var data = new byte[16000]; // 1 second of 16kHz mono audio
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 256);
        }
        var timestamp = DateTime.UtcNow;

        // Act
        var args = new AudioDataEventArgs(data, timestamp);

        // Assert
        Assert.Equal(16000, args.Data.Length);
        Assert.Equal(data, args.Data);
    }

    [Fact]
    public void Data_ReturnsSameReference()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03 };
        var args = new AudioDataEventArgs(data, DateTime.UtcNow);

        // Act & Assert
        Assert.Same(data, args.Data);
    }
}
