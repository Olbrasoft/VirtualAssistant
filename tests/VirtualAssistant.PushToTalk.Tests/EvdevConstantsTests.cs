using Olbrasoft.VirtualAssistant.PushToTalk;

namespace VirtualAssistant.PushToTalk.Tests;

public class EvdevConstantsTests
{
    [Fact]
    public void EVIOCGRAB_HasCorrectValue()
    {
        // Assert - Value from linux/input.h _IOW('E', 0x90, int)
        Assert.Equal(0x40044590u, EvdevConstants.EVIOCGRAB);
    }

    [Fact]
    public void InputEventSize_Is24Bytes()
    {
        // Assert - 64-bit Linux: timeval(16) + type(2) + code(2) + value(4)
        Assert.Equal(24, EvdevConstants.InputEventSize);
    }

    [Fact]
    public void ButtonCodes_HaveCorrectValues()
    {
        // Assert - From linux/input-event-codes.h
        Assert.Equal(272, EvdevConstants.BTN_LEFT);   // 0x110
        Assert.Equal(273, EvdevConstants.BTN_RIGHT);  // 0x111
        Assert.Equal(274, EvdevConstants.BTN_MIDDLE); // 0x112
    }

    [Fact]
    public void KeyValues_HaveCorrectValues()
    {
        Assert.Equal(0, EvdevConstants.KEY_RELEASE);
        Assert.Equal(1, EvdevConstants.KEY_PRESS);
    }

    [Fact]
    public void EV_KEY_HasCorrectValue()
    {
        Assert.Equal(0x01, EvdevConstants.EV_KEY);
    }

    [Fact]
    public void DevicesPath_IsCorrect()
    {
        Assert.Equal("/proc/bus/input/devices", EvdevConstants.DevicesPath);
    }
}
