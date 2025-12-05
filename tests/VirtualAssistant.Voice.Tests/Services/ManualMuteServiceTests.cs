using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace VirtualAssistant.Voice.Tests.Services;

public class ManualMuteServiceTests
{
    private readonly Mock<ILogger<ManualMuteService>> _loggerMock;
    private readonly Mock<IOptions<ContinuousListenerOptions>> _optionsMock;
    private readonly ManualMuteService _sut;

    public ManualMuteServiceTests()
    {
        _loggerMock = new Mock<ILogger<ManualMuteService>>();
        _optionsMock = new Mock<IOptions<ContinuousListenerOptions>>();
        _optionsMock.Setup(x => x.Value).Returns(new ContinuousListenerOptions { StartMuted = false });
        _sut = new ManualMuteService(_loggerMock.Object, _optionsMock.Object);
    }

    [Fact]
    public void IsMuted_InitialState_ReturnsFalse()
    {
        // Act
        var result = _sut.IsMuted;
        
        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Toggle_FromUnmuted_ReturnsTrueAndSetsMuted()
    {
        // Arrange - initial state is unmuted
        Assert.False(_sut.IsMuted);
        
        // Act
        var result = _sut.Toggle();
        
        // Assert
        Assert.True(result);
        Assert.True(_sut.IsMuted);
    }

    [Fact]
    public void Toggle_FromMuted_ReturnsFalseAndSetsUnmuted()
    {
        // Arrange
        _sut.Toggle(); // First toggle to muted
        Assert.True(_sut.IsMuted);
        
        // Act
        var result = _sut.Toggle();
        
        // Assert
        Assert.False(result);
        Assert.False(_sut.IsMuted);
    }

    [Fact]
    public void Toggle_RaisesEvent()
    {
        // Arrange
        bool? eventRaised = null;
        _sut.MuteStateChanged += (sender, isMuted) => eventRaised = isMuted;
        
        // Act
        _sut.Toggle();
        
        // Assert
        Assert.NotNull(eventRaised);
        Assert.True(eventRaised);
    }

    [Fact]
    public void Toggle_MultipleToggles_AlternatesState()
    {
        // Act & Assert
        Assert.True(_sut.Toggle());   // false -> true
        Assert.False(_sut.Toggle());  // true -> false
        Assert.True(_sut.Toggle());   // false -> true
        Assert.False(_sut.Toggle());  // true -> false
    }

    [Fact]
    public void SetMuted_ToTrue_SetsMutedState()
    {
        // Arrange
        Assert.False(_sut.IsMuted);
        
        // Act
        _sut.SetMuted(true);
        
        // Assert
        Assert.True(_sut.IsMuted);
    }

    [Fact]
    public void SetMuted_ToFalse_SetsUnmutedState()
    {
        // Arrange
        _sut.SetMuted(true);
        Assert.True(_sut.IsMuted);
        
        // Act
        _sut.SetMuted(false);
        
        // Assert
        Assert.False(_sut.IsMuted);
    }

    [Fact]
    public void SetMuted_SameState_DoesNotRaiseEvent()
    {
        // Arrange
        var eventCount = 0;
        _sut.MuteStateChanged += (sender, isMuted) => eventCount++;
        
        // Act
        _sut.SetMuted(false); // Already false
        _sut.SetMuted(false); // Still false
        
        // Assert
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void SetMuted_DifferentState_RaisesEvent()
    {
        // Arrange
        bool? lastState = null;
        _sut.MuteStateChanged += (sender, isMuted) => lastState = isMuted;
        
        // Act
        _sut.SetMuted(true);
        
        // Assert
        Assert.NotNull(lastState);
        Assert.True(lastState);
    }

    [Fact]
    public void SetMuted_MultipleChanges_RaisesEventForEachChange()
    {
        // Arrange
        var eventCount = 0;
        _sut.MuteStateChanged += (sender, isMuted) => eventCount++;
        
        // Act
        _sut.SetMuted(true);   // Event 1
        _sut.SetMuted(false);  // Event 2
        _sut.SetMuted(true);   // Event 3
        
        // Assert
        Assert.Equal(3, eventCount);
    }

    [Fact]
    public async Task Toggle_IsThreadSafe()
    {
        // Arrange
        var iterations = 1000;
        var tasks = new Task[10];
        
        // Act - multiple threads toggling simultaneously
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < iterations; j++)
                {
                    _sut.Toggle();
                }
            });
        }
        
        await Task.WhenAll(tasks);
        
        // Assert - no exception means thread safety works
        // Final state depends on total toggle count (even = false, odd = true)
        var expectedState = (tasks.Length * iterations) % 2 == 1;
        Assert.Equal(expectedState, _sut.IsMuted);
    }
}
