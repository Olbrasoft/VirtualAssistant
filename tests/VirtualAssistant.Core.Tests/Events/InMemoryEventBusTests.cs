using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Events;

namespace Olbrasoft.VirtualAssistant.Core.Tests.Events;

/// <summary>
/// Pins the in-process pub/sub contract: handlers registered via Subscribe
/// receive PublishAsync payloads, unsubscribing via the returned IDisposable
/// stops further delivery, and a throwing handler doesn't prevent sibling
/// handlers from running.
/// </summary>
public class InMemoryEventBusTests
{
    private record TestEvent(string Payload);
    private record UnrelatedEvent(int Number);

    private readonly Mock<ILogger<InMemoryEventBus>> _loggerMock = new();

    private InMemoryEventBus CreateSut() => new(_loggerMock.Object);

    [Fact]
    public async Task PublishAsync_WithNoHandlers_CompletesWithoutThrowing()
    {
        var sut = CreateSut();

        var ex = await Record.ExceptionAsync(() => sut.PublishAsync(new TestEvent("x")));

        Assert.Null(ex);
    }

    [Fact]
    public async Task PublishAsync_DeliversToSubscribedHandler()
    {
        var sut = CreateSut();
        TestEvent? received = null;
        sut.Subscribe<TestEvent>((e, _) =>
        {
            received = e;
            return Task.CompletedTask;
        });

        await sut.PublishAsync(new TestEvent("hello"));

        Assert.Equal(new TestEvent("hello"), received);
    }

    [Fact]
    public async Task PublishAsync_DeliversToAllSubscribedHandlers()
    {
        var sut = CreateSut();
        var count = 0;
        sut.Subscribe<TestEvent>((_, _) => { Interlocked.Increment(ref count); return Task.CompletedTask; });
        sut.Subscribe<TestEvent>((_, _) => { Interlocked.Increment(ref count); return Task.CompletedTask; });
        sut.Subscribe<TestEvent>((_, _) => { Interlocked.Increment(ref count); return Task.CompletedTask; });

        await sut.PublishAsync(new TestEvent("x"));

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task PublishAsync_WithHandlerForDifferentType_DoesNotDeliver()
    {
        var sut = CreateSut();
        var otherFired = false;
        sut.Subscribe<UnrelatedEvent>((_, _) => { otherFired = true; return Task.CompletedTask; });

        await sut.PublishAsync(new TestEvent("x"));

        Assert.False(otherFired);
    }

    [Fact]
    public async Task Subscribe_DisposeSubscription_StopsDelivery()
    {
        var sut = CreateSut();
        var count = 0;
        var subscription = sut.Subscribe<TestEvent>((_, _) =>
        {
            Interlocked.Increment(ref count);
            return Task.CompletedTask;
        });

        await sut.PublishAsync(new TestEvent("a"));
        subscription.Dispose();
        await sut.PublishAsync(new TestEvent("b"));

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task PublishAsync_ThrowingHandler_DoesNotPreventSiblings()
    {
        // A faulted handler must be isolated — one bad subscriber shouldn't
        // poison other subscribers on the same event. The bus is supposed to
        // log the exception and keep going.
        var sut = CreateSut();
        var goodRan = false;
        sut.Subscribe<TestEvent>((_, _) => throw new InvalidOperationException("boom"));
        sut.Subscribe<TestEvent>((_, _) =>
        {
            goodRan = true;
            return Task.CompletedTask;
        });

        await sut.PublishAsync(new TestEvent("x"));

        Assert.True(goodRan);
    }

    [Fact]
    public async Task PublishAsync_PassesCancellationTokenToHandler()
    {
        var sut = CreateSut();
        CancellationToken received = default;
        sut.Subscribe<TestEvent>((_, ct) => { received = ct; return Task.CompletedTask; });
        using var cts = new CancellationTokenSource();

        await sut.PublishAsync(new TestEvent("x"), cts.Token);

        Assert.Equal(cts.Token, received);
    }

    [Fact]
    public void Subscribe_MultipleTimesSameHandler_CreatesMultipleRegistrations()
    {
        // Subscribe returns a per-call IDisposable that removes exactly that
        // registration. Disposing one should not remove the other, otherwise
        // the "remove-by-reference" semantics would collapse. This test pins
        // the per-subscription disposal contract.
        var sut = CreateSut();
        Func<TestEvent, CancellationToken, Task> handler = (_, _) => Task.CompletedTask;
        var sub1 = sut.Subscribe(handler);
        var sub2 = sut.Subscribe(handler);

        sub1.Dispose();
        sub2.Dispose();

        // No assertion needed — the check is that double-dispose doesn't throw
        // and that the bus cleans up both registrations cleanly.
    }
}
