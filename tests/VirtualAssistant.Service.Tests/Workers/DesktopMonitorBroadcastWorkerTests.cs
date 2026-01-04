using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Service.Hubs;
using Olbrasoft.VirtualAssistant.Service.Workers;
using System.Reactive.Subjects;
using VirtualAssistant.Core.Models;
using VirtualAssistant.Core.Services;

namespace VirtualAssistant.Service.Tests.Workers;

/// <summary>
/// Unit tests for DesktopMonitorBroadcastWorker.
/// Tests SignalR broadcasting of desktop context changes.
/// </summary>
public class DesktopMonitorBroadcastWorkerTests : IDisposable
{
    private readonly Mock<ILogger<DesktopMonitorBroadcastWorker>> _loggerMock;
    private readonly Mock<IDesktopContextService> _desktopContextServiceMock;
    private readonly Mock<IHubContext<DesktopMonitorHub>> _hubContextMock;
    private readonly Mock<IHubClients> _hubClientsMock;
    private readonly Mock<IClientProxy> _clientProxyMock;
    private readonly Subject<DesktopContextChange> _contextChangesSubject;
    private readonly DesktopMonitorBroadcastWorker _sut;

    public DesktopMonitorBroadcastWorkerTests()
    {
        _loggerMock = new Mock<ILogger<DesktopMonitorBroadcastWorker>>();
        _desktopContextServiceMock = new Mock<IDesktopContextService>();
        _hubContextMock = new Mock<IHubContext<DesktopMonitorHub>>();
        _hubClientsMock = new Mock<IHubClients>();
        _clientProxyMock = new Mock<IClientProxy>();

        // Setup Subject for observable stream
        _contextChangesSubject = new Subject<DesktopContextChange>();

        // Setup mocks
        _desktopContextServiceMock.Setup(x => x.ContextChanges)
            .Returns(_contextChangesSubject);

        _hubContextMock.Setup(x => x.Clients)
            .Returns(_hubClientsMock.Object);

        _hubClientsMock.Setup(x => x.All)
            .Returns(_clientProxyMock.Object);

        _sut = new DesktopMonitorBroadcastWorker(
            _loggerMock.Object,
            _desktopContextServiceMock.Object,
            _hubContextMock.Object
        );
    }

    [Fact]
    public async Task ExecuteAsync_SubscribesToContextChanges()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act
        _ = _sut.StartAsync(cts.Token);

        // Wait for subscription with polling (deterministic)
        await WaitForCondition(() =>
            _desktopContextServiceMock.Invocations.Any(i => i.Method.Name == "get_ContextChanges"),
            timeout: TimeSpan.FromSeconds(2)
        );

        // Assert
        _desktopContextServiceMock.Verify(x => x.ContextChanges, Times.Once);
    }

    [Fact]
    public async Task WorkspaceChanged_BroadcastsWorkspaceChangedEvent()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await WaitForCondition(() =>
            _desktopContextServiceMock.Invocations.Any(i => i.Method.Name == "get_ContextChanges"),
            timeout: TimeSpan.FromSeconds(2)
        );

        var oldContext = new DesktopContext(0, 4, "Old Window", "old-class", "old-app", DateTime.UtcNow);
        var newContext = new DesktopContext(1, 4, "New Window", "new-class", "new-app", DateTime.UtcNow);
        var change = new DesktopContextChange(oldContext, newContext, ChangeType.WorkspaceChanged);

        // Act
        _contextChangesSubject.OnNext(change);

        // Wait for broadcast with polling (deterministic)
        await WaitForCondition(() =>
            _clientProxyMock.Invocations.Any(i =>
                i.Method.Name == "SendCoreAsync" &&
                i.Arguments[0] as string == "WorkspaceChanged"),
            timeout: TimeSpan.FromSeconds(2)
        );

        // Assert - workspace is 1-based for UI display (CurrentWorkspace 1 becomes 2)
        _clientProxyMock.Verify(
            x => x.SendCoreAsync(
                "WorkspaceChanged",
                It.Is<object[]>(args => (int)args[0] == 2 && (int)args[1] == 4),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task ApplicationChanged_BroadcastsFocusChangedEvent()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await WaitForCondition(() =>
            _desktopContextServiceMock.Invocations.Any(i => i.Method.Name == "get_ContextChanges"),
            timeout: TimeSpan.FromSeconds(2)
        );

        var oldContext = new DesktopContext(0, 4, "Old Window", "old-class", "old-app", DateTime.UtcNow);
        var newContext = new DesktopContext(0, 4, "New Window", "new-class", "new-app", DateTime.UtcNow);
        var change = new DesktopContextChange(oldContext, newContext, ChangeType.ApplicationChanged);

        // Act
        _contextChangesSubject.OnNext(change);

        // Wait for broadcast with polling (deterministic)
        await WaitForCondition(() =>
            _clientProxyMock.Invocations.Any(i =>
                i.Method.Name == "SendCoreAsync" &&
                i.Arguments[0] as string == "FocusChanged"),
            timeout: TimeSpan.FromSeconds(2)
        );

        // Assert
        _clientProxyMock.Verify(
            x => x.SendCoreAsync(
                "FocusChanged",
                It.Is<object[]>(args =>
                    (string)args[0] == "New Window" &&
                    (string)args[1] == "new-app" &&
                    (string)args[2] == "new-class"
                ),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task WindowFocusChanged_BroadcastsFocusChangedEvent()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await WaitForCondition(() =>
            _desktopContextServiceMock.Invocations.Any(i => i.Method.Name == "get_ContextChanges"),
            timeout: TimeSpan.FromSeconds(2)
        );

        var oldContext = new DesktopContext(0, 4, "Old Title", "same-class", "same-app", DateTime.UtcNow);
        var newContext = new DesktopContext(0, 4, "New Title", "same-class", "same-app", DateTime.UtcNow);
        var change = new DesktopContextChange(oldContext, newContext, ChangeType.WindowFocusChanged);

        // Act
        _contextChangesSubject.OnNext(change);

        // Wait for broadcast with polling (deterministic)
        await WaitForCondition(() =>
            _clientProxyMock.Invocations.Any(i =>
                i.Method.Name == "SendCoreAsync" &&
                i.Arguments[0] as string == "FocusChanged"),
            timeout: TimeSpan.FromSeconds(2)
        );

        // Assert
        _clientProxyMock.Verify(
            x => x.SendCoreAsync(
                "FocusChanged",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task ContextChange_BroadcastsLogMessage()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await WaitForCondition(() =>
            _desktopContextServiceMock.Invocations.Any(i => i.Method.Name == "get_ContextChanges"),
            timeout: TimeSpan.FromSeconds(2)
        );

        var oldContext = new DesktopContext(0, 4, "Old", "old", "old", DateTime.UtcNow);
        var newContext = new DesktopContext(1, 4, "New", "new", "new", DateTime.UtcNow);
        var change = new DesktopContextChange(oldContext, newContext, ChangeType.WorkspaceChanged);

        // Act
        _contextChangesSubject.OnNext(change);

        // Wait for broadcast with polling (deterministic)
        await WaitForCondition(() =>
            _clientProxyMock.Invocations.Any(i =>
                i.Method.Name == "SendCoreAsync" &&
                i.Arguments[0] as string == "LogMessage"),
            timeout: TimeSpan.FromSeconds(2)
        );

        // Assert - LogMessage should be broadcasted
        _clientProxyMock.Verify(
            x => x.SendCoreAsync(
                "LogMessage",
                It.Is<object[]>(args => ((string)args[0]).Contains("WorkspaceChanged")),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task StopAsync_DisposesSubscription()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await WaitForCondition(() =>
            _desktopContextServiceMock.Invocations.Any(i => i.Method.Name == "get_ContextChanges"),
            timeout: TimeSpan.FromSeconds(2)
        );

        // Act
        await _sut.StopAsync(CancellationToken.None);

        // Push event after stop
        var context = new DesktopContext(0, 4, "Test", "test", "test", DateTime.UtcNow);
        var change = new DesktopContextChange(context, context, ChangeType.WorkspaceChanged);
        _contextChangesSubject.OnNext(change);

        // Give a small delay to ensure event would have been processed if subscription was active
        // Using small delay here is acceptable as we're testing negative case (should NOT happen)
        await Task.Delay(100);

        // Assert - Should not broadcast after stop (only initial subscribe call)
        _clientProxyMock.Verify(
            x => x.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Never // No broadcasts should happen after stop
        );
    }

    /// <summary>
    /// Waits for a condition to be true with polling and timeout.
    /// Avoids flaky tests that rely on arbitrary Task.Delay values.
    /// </summary>
    private static async Task WaitForCondition(Func<bool> condition, TimeSpan timeout, int pollIntervalMs = 10)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.Elapsed > timeout)
            {
                throw new TimeoutException($"Condition not met within {timeout.TotalSeconds}s");
            }
            await Task.Delay(pollIntervalMs);
        }
    }

    public void Dispose()
    {
        _contextChangesSubject?.Dispose();
        // Avoid deadlock - use async Dispose pattern
        _sut?.StopAsync(CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
    }
}
