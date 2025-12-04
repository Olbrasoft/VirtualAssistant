using Olbrasoft.VirtualAssistant.PushToTalk;

namespace VirtualAssistant.PushToTalk.Tests;

public class KeyEventArgsTests
{
    [Fact]
    public void Constructor_ValidParameters_SetsAllProperties()
    {
        // Arrange
        var key = KeyCode.CapsLock;
        var rawKeyCode = 58;
        var timestamp = new DateTime(2025, 1, 1, 12, 0, 0);

        // Act
        var args = new KeyEventArgs(key, rawKeyCode, timestamp);

        // Assert
        Assert.Equal(key, args.Key);
        Assert.Equal(rawKeyCode, args.RawKeyCode);
        Assert.Equal(timestamp, args.Timestamp);
    }

    [Theory]
    [InlineData(KeyCode.CapsLock, 58)]
    [InlineData(KeyCode.ScrollLock, 70)]
    [InlineData(KeyCode.NumLock, 69)]
    [InlineData(KeyCode.Escape, 1)]
    [InlineData(KeyCode.Space, 57)]
    public void Constructor_DifferentKeyCodes_PreservesValues(KeyCode key, int rawCode)
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        // Act
        var args = new KeyEventArgs(key, rawCode, timestamp);

        // Assert
        Assert.Equal(key, args.Key);
        Assert.Equal(rawCode, args.RawKeyCode);
    }

    [Fact]
    public void Constructor_UnknownKeyCode_SetsUnknown()
    {
        // Arrange
        var key = KeyCode.Unknown;
        var rawKeyCode = 999;
        var timestamp = DateTime.UtcNow;

        // Act
        var args = new KeyEventArgs(key, rawKeyCode, timestamp);

        // Assert
        Assert.Equal(KeyCode.Unknown, args.Key);
        Assert.Equal(999, args.RawKeyCode);
    }
}
