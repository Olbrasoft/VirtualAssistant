using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Events;
using Xunit;

namespace VirtualAssistant.Core.Tests.Events;

public class EventBusTests
{
    private readonly Mock<ILogger<EventBus>> _mockLogger;
    private readonly EventBus _eventBus;

    public EventBusTests()
    {
        _mockLogger = new Mock<ILogger<EventBus>>();
        _eventBus = new EventBus(_mockLogger.Object);
    }

    [Fact]
    public async Task PublishAsync_WithNoSubscribers_DoesNotThrow()
    {
        // Arrange
        var @event = new MuteStateChangedEvent(true);

        // Act & Assert
        await _eventBus.PublishAsync(@event);
    }

    [Fact]
    public async Task PublishAsync_WithOneSubscriber_InvokesHandler()
    {
        // Arrange
        var handlerCalled = false;
        MuteStateChangedEvent? receivedEvent = null;

        _eventBus.Subscribe<MuteStateChangedEvent>(e =>
        {
            handlerCalled = true;
            receivedEvent = e;
        });

        var @event = new MuteStateChangedEvent(true);

        // Act
        await _eventBus.PublishAsync(@event);

        // Assert
        Assert.True(handlerCalled);
        Assert.NotNull(receivedEvent);
        Assert.True(receivedEvent.IsMuted);
    }

    [Fact]
    public async Task PublishAsync_WithMultipleSubscribers_InvokesAllHandlers()
    {
        // Arrange
        var handler1Called = false;
        var handler2Called = false;

        _eventBus.Subscribe<MuteStateChangedEvent>(_ => handler1Called = true);
        _eventBus.Subscribe<MuteStateChangedEvent>(_ => handler2Called = true);

        var @event = new MuteStateChangedEvent(true);

        // Act
        await _eventBus.PublishAsync(@event);

        // Assert
        Assert.True(handler1Called);
        Assert.True(handler2Called);
    }

    [Fact]
    public async Task PublishAsync_WithDisposedSubscription_DoesNotInvokeHandler()
    {
        // Arrange
        var handlerCalled = false;

        var subscription = _eventBus.Subscribe<MuteStateChangedEvent>(_ => handlerCalled = true);
        subscription.Dispose();

        var @event = new MuteStateChangedEvent(true);

        // Act
        await _eventBus.PublishAsync(@event);

        // Assert
        Assert.False(handlerCalled);
    }

    [Fact]
    public async Task PublishAsync_WithHandlerException_ContinuesInvokingOtherHandlers()
    {
        // Arrange
        var handler2Called = false;

        _eventBus.Subscribe<MuteStateChangedEvent>(_ => throw new InvalidOperationException("Test exception"));
        _eventBus.Subscribe<MuteStateChangedEvent>(_ => handler2Called = true);

        var @event = new MuteStateChangedEvent(true);

        // Act
        await _eventBus.PublishAsync(@event);

        // Assert
        Assert.True(handler2Called);
    }

    [Fact]
    public async Task PublishAsync_WithDifferentEventTypes_InvokesCorrectHandlers()
    {
        // Arrange
        var muteHandlerCalled = false;
        var keyHandlerCalled = false;

        _eventBus.Subscribe<MuteStateChangedEvent>(_ => muteHandlerCalled = true);
        _eventBus.Subscribe<KeyPressedEvent>(_ => keyHandlerCalled = true);

        var muteEvent = new MuteStateChangedEvent(true);

        // Act
        await _eventBus.PublishAsync(muteEvent);

        // Assert
        Assert.True(muteHandlerCalled);
        Assert.False(keyHandlerCalled);
    }

    [Fact]
    public void Subscribe_WithNullHandler_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _eventBus.Subscribe<MuteStateChangedEvent>(null!));
    }

    [Fact]
    public async Task PublishAsync_WithNullEvent_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _eventBus.PublishAsync<MuteStateChangedEvent>(null!));
    }

    [Fact]
    public async Task PublishAsync_IsConcurrentSafe()
    {
        // Arrange
        var callCount = 0;
        var lockObj = new object();

        _eventBus.Subscribe<MuteStateChangedEvent>(_ =>
        {
            lock (lockObj)
            {
                callCount++;
            }
        });

        // Act - publish 100 events concurrently
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => _eventBus.PublishAsync(new MuteStateChangedEvent(true)));

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(100, callCount);
    }

    [Fact]
    public async Task Dispose_MultipleSubscriptions_UnsubscribesCorrectly()
    {
        // Arrange
        var handler1Called = false;
        var handler2Called = false;

        var subscription1 = _eventBus.Subscribe<MuteStateChangedEvent>(_ => handler1Called = true);
        var subscription2 = _eventBus.Subscribe<MuteStateChangedEvent>(_ => handler2Called = true);

        // Act
        subscription1.Dispose();

        // Assert - only subscription2 should be called
        await _eventBus.PublishAsync(new MuteStateChangedEvent(true));
        Assert.False(handler1Called);
        Assert.True(handler2Called);
    }
}
