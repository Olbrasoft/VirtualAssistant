using Olbrasoft.VirtualAssistant.PushToTalk;

namespace VirtualAssistant.PushToTalk.Tests;

public class KeyCodeTests
{
    [Fact]
    public void KeyCode_CapsLock_HasCorrectValue()
    {
        // Assert - based on Linux input-event-codes.h
        Assert.Equal(58, (int)KeyCode.CapsLock);
    }

    [Fact]
    public void KeyCode_ScrollLock_HasCorrectValue()
    {
        Assert.Equal(70, (int)KeyCode.ScrollLock);
    }

    [Fact]
    public void KeyCode_NumLock_HasCorrectValue()
    {
        Assert.Equal(69, (int)KeyCode.NumLock);
    }

    [Fact]
    public void KeyCode_Escape_HasCorrectValue()
    {
        Assert.Equal(1, (int)KeyCode.Escape);
    }

    [Fact]
    public void KeyCode_Unknown_HasZeroValue()
    {
        Assert.Equal(0, (int)KeyCode.Unknown);
    }

    [Fact]
    public void KeyCode_Space_HasCorrectValue()
    {
        Assert.Equal(57, (int)KeyCode.Space);
    }

    [Fact]
    public void KeyCode_Enter_HasCorrectValue()
    {
        Assert.Equal(28, (int)KeyCode.Enter);
    }

    [Fact]
    public void KeyCode_LeftControl_HasCorrectValue()
    {
        Assert.Equal(29, (int)KeyCode.LeftControl);
    }

    [Fact]
    public void KeyCode_RightControl_HasCorrectValue()
    {
        Assert.Equal(97, (int)KeyCode.RightControl);
    }

    [Fact]
    public void KeyCode_LeftShift_HasCorrectValue()
    {
        Assert.Equal(42, (int)KeyCode.LeftShift);
    }

    [Fact]
    public void KeyCode_RightShift_HasCorrectValue()
    {
        Assert.Equal(54, (int)KeyCode.RightShift);
    }

    [Fact]
    public void KeyCode_LeftAlt_HasCorrectValue()
    {
        Assert.Equal(56, (int)KeyCode.LeftAlt);
    }

    [Fact]
    public void KeyCode_RightAlt_HasCorrectValue()
    {
        Assert.Equal(100, (int)KeyCode.RightAlt);
    }

    [Theory]
    [InlineData(58, KeyCode.CapsLock)]
    [InlineData(70, KeyCode.ScrollLock)]
    [InlineData(69, KeyCode.NumLock)]
    [InlineData(1, KeyCode.Escape)]
    [InlineData(0, KeyCode.Unknown)]
    public void KeyCode_CastFromInt_ReturnsCorrectEnum(int value, KeyCode expected)
    {
        // Act
        var result = (KeyCode)value;

        // Assert
        Assert.Equal(expected, result);
    }
}
