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
    public async Task Subscribe_TwoDistinctLambdasSameBehavior_DisposingOneKeepsOther()
    {
        // Pins the per-subscription disposal contract via two DISTINCT
        // lambdas — InMemoryEventBus.Unsubscribe uses ReferenceEquals, so
        // if we'd subscribed the same delegate variable twice a single
        // Dispose would remove both registrations. Two separately-allocated
        // lambdas that happen to share state (`callCount`) give us the
        // isolated-registrations scenario the test intends to pin.
        var sut = CreateSut();
        var callCount = 0;
        var sub1 = sut.Subscribe<TestEvent>((_, _) =>
        {
            Interlocked.Increment(ref callCount);
            return Task.CompletedTask;
        });
        var sub2 = sut.Subscribe<TestEvent>((_, _) =>
        {
            Interlocked.Increment(ref callCount);
            return Task.CompletedTask;
        });

        sub1.Dispose();
        await sut.PublishAsync(new TestEvent("x"));

        Assert.Equal(1, callCount);
        sub2.Dispose();
    }

    [Fact]
    public async Task Subscribe_SameDelegateTwice_DisposingOneRemovesBoth()
    {
        // Documents the current InMemoryEventBus behaviour for the
        // "subscribe the same delegate twice" edge case: Unsubscribe uses
        // ReferenceEquals, so a single Dispose removes every registration
        // that shares the delegate instance. This is arguably a latent
        // bug (per-subscription tokens should be independent), but pinning
        // the current behaviour prevents silent regressions and makes a
        // future fix visible as a test breakage. CI on commit 5f08bc1
        // failed because an earlier version of the adjacent test assumed
        // the opposite semantics.
        var sut = CreateSut();
        var callCount = 0;
        Func<TestEvent, CancellationToken, Task> handler = (_, _) =>
        {
            Interlocked.Increment(ref callCount);
            return Task.CompletedTask;
        };
        var sub1 = sut.Subscribe(handler);
        var sub2 = sut.Subscribe(handler);

        sub1.Dispose();
        await sut.PublishAsync(new TestEvent("x"));

        Assert.Equal(0, callCount);
        sub2.Dispose();
    }
}
