using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.PushToTalk.Service.Services;

namespace VirtualAssistant.PushToTalk.Service.Tests;

public class ManualMuteServiceTests
{
    private readonly Mock<ILogger<ManualMuteService>> _loggerMock;
    private readonly ManualMuteService _service;

    public ManualMuteServiceTests()
    {
        _loggerMock = new Mock<ILogger<ManualMuteService>>();
        _service = new ManualMuteService(_loggerMock.Object);
    }

    [Fact]
    public void IsMuted_InitialState_ReturnsFalse()
    {
        // Assert
        Assert.False(_service.IsMuted);
    }

    [Fact]
    public void Toggle_FromUnmuted_ReturnsTrueAndSetsMuted()
    {
        // Act
        var result = _service.Toggle();

        // Assert
        Assert.True(result);
        Assert.True(_service.IsMuted);
    }

    [Fact]
    public void Toggle_FromMuted_ReturnsFalseAndSetsUnmuted()
    {
        // Arrange
        _service.Toggle(); // First toggle to muted

        // Act
        var result = _service.Toggle(); // Second toggle to unmuted

        // Assert
        Assert.False(result);
        Assert.False(_service.IsMuted);
    }

    [Fact]
    public void Toggle_MultipleTimes_AlternatesState()
    {
        // Act & Assert
        Assert.True(_service.Toggle());  // Muted
        Assert.False(_service.Toggle()); // Unmuted
        Assert.True(_service.Toggle());  // Muted
        Assert.False(_service.Toggle()); // Unmuted
    }

    [Fact]
    public void SetMuted_ToTrue_SetsMutedState()
    {
        // Act
        _service.SetMuted(true);

        // Assert
        Assert.True(_service.IsMuted);
    }

    [Fact]
    public void SetMuted_ToFalse_SetsUnmutedState()
    {
        // Arrange
        _service.SetMuted(true);

        // Act
        _service.SetMuted(false);

        // Assert
        Assert.False(_service.IsMuted);
    }

    [Fact]
    public void SetMuted_ToSameValue_DoesNotTriggerEvent()
    {
        // Arrange
        var eventCount = 0;
        _service.MuteStateChanged += (_, _) => eventCount++;

        // Act
        _service.SetMuted(false); // Already false
        _service.SetMuted(false); // Still false

        // Assert
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void Toggle_RaisesEvent_WithNewState()
    {
        // Arrange
        bool? receivedState = null;
        _service.MuteStateChanged += (_, state) => receivedState = state;

        // Act
        _service.Toggle();

        // Assert
        Assert.NotNull(receivedState);
        Assert.True(receivedState.Value);
    }

    [Fact]
    public void SetMuted_RaisesEvent_WhenStateChanges()
    {
        // Arrange
        bool? receivedState = null;
        _service.MuteStateChanged += (_, state) => receivedState = state;

        // Act
        _service.SetMuted(true);

        // Assert
        Assert.NotNull(receivedState);
        Assert.True(receivedState.Value);
    }

    [Fact]
    public async Task Toggle_IsThreadSafe_MultipleCalls()
    {
        // Arrange
        var toggleCount = 100;
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < toggleCount; i++)
        {
            tasks.Add(Task.Run(() => _service.Toggle()));
        }

        await Task.WhenAll(tasks);

        // Assert - after even number of toggles, should be back to initial state
        Assert.False(_service.IsMuted);
    }

    [Fact]
    public void MuteStateChanged_EventSender_IsService()
    {
        // Arrange
        object? receivedSender = null;
        _service.MuteStateChanged += (sender, _) => receivedSender = sender;

        // Act
        _service.Toggle();

        // Assert
        Assert.Same(_service, receivedSender);
    }
}
