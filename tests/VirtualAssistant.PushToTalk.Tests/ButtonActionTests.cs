using Moq;
using Olbrasoft.VirtualAssistant.PushToTalk;

namespace VirtualAssistant.PushToTalk.Tests;

public class ButtonActionTests
{
    private readonly Mock<IKeyboardMonitor> _mockKeyboardMonitor;

    public ButtonActionTests()
    {
        _mockKeyboardMonitor = new Mock<IKeyboardMonitor>();
    }

    [Fact]
    public async Task KeyPressAction_ExecuteAsync_SimulatesKeyPress()
    {
        // Arrange
        var action = new KeyPressAction(_mockKeyboardMonitor.Object, KeyCode.CapsLock, "CapsLock");

        // Act
        await action.ExecuteAsync();

        // Assert
        _mockKeyboardMonitor.Verify(x => x.SimulateKeyPressAsync(KeyCode.CapsLock), Times.Once);
        _mockKeyboardMonitor.Verify(x => x.RaiseKeyReleasedEvent(KeyCode.CapsLock), Times.Once);
    }

    [Fact]
    public async Task KeyPressAction_WithoutReleaseEvent_DoesNotRaiseRelease()
    {
        // Arrange
        var action = new KeyPressAction(_mockKeyboardMonitor.Object, KeyCode.Enter, "Enter", raiseReleaseEvent: false);

        // Act
        await action.ExecuteAsync();

        // Assert
        _mockKeyboardMonitor.Verify(x => x.SimulateKeyPressAsync(KeyCode.Enter), Times.Once);
        _mockKeyboardMonitor.Verify(x => x.RaiseKeyReleasedEvent(It.IsAny<KeyCode>()), Times.Never);
    }

    [Fact]
    public async Task KeyComboAction_ExecuteAsync_SimulatesKeyCombo()
    {
        // Arrange
        var action = new KeyComboAction(_mockKeyboardMonitor.Object, KeyCode.LeftControl, KeyCode.C, "Ctrl+C");

        // Act
        await action.ExecuteAsync();

        // Assert
        _mockKeyboardMonitor.Verify(x => x.SimulateKeyComboAsync(KeyCode.LeftControl, KeyCode.C), Times.Once);
    }

    [Fact]
    public async Task KeyComboWithTwoModifiersAction_ExecuteAsync_SimulatesThreeKeyCombo()
    {
        // Arrange
        var action = new KeyComboWithTwoModifiersAction(
            _mockKeyboardMonitor.Object,
            KeyCode.LeftControl,
            KeyCode.LeftShift,
            KeyCode.V,
            "Ctrl+Shift+V");

        // Act
        await action.ExecuteAsync();

        // Assert
        _mockKeyboardMonitor.Verify(
            x => x.SimulateKeyComboAsync(KeyCode.LeftControl, KeyCode.LeftShift, KeyCode.V),
            Times.Once);
    }

    [Fact]
    public async Task NoAction_ExecuteAsync_DoesNothing()
    {
        // Arrange
        var action = NoAction.Instance;

        // Act
        await action.ExecuteAsync();

        // Assert
        Assert.Equal("NoAction", action.Name);
    }

    [Fact]
    public void NoAction_IsSingleton()
    {
        // Assert
        Assert.Same(NoAction.Instance, NoAction.Instance);
    }

    [Fact]
    public void KeyPressAction_HasCorrectName()
    {
        // Arrange
        var action = new KeyPressAction(_mockKeyboardMonitor.Object, KeyCode.Escape, "ESC");

        // Assert
        Assert.Equal("ESC", action.Name);
    }

    [Fact]
    public void KeyComboAction_HasCorrectName()
    {
        // Arrange
        var action = new KeyComboAction(_mockKeyboardMonitor.Object, KeyCode.LeftControl, KeyCode.C, "Ctrl+C (copy)");

        // Assert
        Assert.Equal("Ctrl+C (copy)", action.Name);
    }

    [Fact]
    public void ShellCommandAction_HasCorrectName()
    {
        // Arrange
        var action = new ShellCommandAction("/bin/test", "Test command");

        // Assert
        Assert.Equal("Test command", action.Name);
    }
}
