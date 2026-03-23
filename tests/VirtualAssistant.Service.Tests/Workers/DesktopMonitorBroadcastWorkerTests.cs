using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.Testing.Xunit.Attributes;
using Olbrasoft.VirtualAssistant.Service.Hubs;
using Olbrasoft.VirtualAssistant.Service.Workers;
using System.Reactive.Subjects;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Workers;

/// <summary>
/// Unit tests for DesktopMonitorBroadcastWorker.
/// Tests SignalR broadcasting of desktop context changes.
/// </summary>
public class DesktopMonitorBroadcastWorkerTests : IDisposable
{
    private readonly Mock<ILogger<DesktopMonitorBroadcastWorker>> _loggerMock;
    private readonly Mock<IDesktopContextService> _desktopContextServiceMock;
    private readonly Mock<IHubContext<DesktopMonitorHub>> _hubContextMock;
    private readonly Mock<IHubContext<DictationHub>> _dictationHubContextMock;
    private readonly Mock<IHubClients> _hubClientsMock;
    private readonly Mock<IClientProxy> _clientProxyMock;
    private readonly Mock<IQueryProcessor> _queryProcessorMock;
    private readonly Mock<ICliAppDetector> _cliAppDetectorMock;
    private readonly Subject<DesktopContextChange> _contextChangesSubject;
    private readonly DesktopMonitorBroadcastWorker _sut;

    public DesktopMonitorBroadcastWorkerTests()
    {
        _loggerMock = new Mock<ILogger<DesktopMonitorBroadcastWorker>>();
        _desktopContextServiceMock = new Mock<IDesktopContextService>();
        _hubContextMock = new Mock<IHubContext<DesktopMonitorHub>>();
        _dictationHubContextMock = new Mock<IHubContext<DictationHub>>();
        _dictationHubContextMock.Setup(x => x.Clients).Returns(Mock.Of<IHubClients>());
        _hubClientsMock = new Mock<IHubClients>();
        _clientProxyMock = new Mock<IClientProxy>();
        _queryProcessorMock = new Mock<IQueryProcessor>();
        _cliAppDetectorMock = new Mock<ICliAppDetector>();

        // Setup Subject for observable stream
        _contextChangesSubject = new Subject<DesktopContextChange>();

        // Setup mocks
        _desktopContextServiceMock.Setup(x => x.ContextChanges)
            .Returns(_contextChangesSubject);

        _hubContextMock.Setup(x => x.Clients)
            .Returns(_hubClientsMock.Object);

        _hubClientsMock.Setup(x => x.All)
            .Returns(_clientProxyMock.Object);

        // By default, no CLI app detected
        _cliAppDetectorMock.Setup(x => x.DetectCliAppAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CliAppDetectionResult?)null);

        _sut = new DesktopMonitorBroadcastWorker(
            _loggerMock.Object,
            _desktopContextServiceMock.Object,
            _hubContextMock.Object,
            _dictationHubContextMock.Object,
            _queryProcessorMock.Object,
            _cliAppDetectorMock.Object
        );
    }

    [Fact]
    public async Task ExecuteAsync_SubscribesToContextChanges()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act
        _ = _sut.StartAsync(cts.Token);
        await Task.Delay(100); // Give worker time to subscribe

        // Assert
        _desktopContextServiceMock.Verify(x => x.ContextChanges, Times.Once);
    }

    [Fact]
    public async Task WorkspaceChanged_BroadcastsWorkspaceChangedEvent()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(100); // Give worker time to subscribe

        var oldContext = new DesktopContext(0, 4, "Old Window", "old-class", "old-app", DateTime.UtcNow);
        var newContext = new DesktopContext(1, 4, "New Window", "new-class", "new-app", DateTime.UtcNow);
        var change = new DesktopContextChange(oldContext, newContext, ChangeType.WorkspaceChanged);

        // Act
        _contextChangesSubject.OnNext(change);
        await Task.Delay(200); // Give worker time to process

        // Assert - workspace indices converted from 0-based (internal) to 1-based (UI display)
        // Example: internal index 1 displays as 2 in UI
        _clientProxyMock.Verify(
            x => x.SendCoreAsync(
                "WorkspaceChanged",
                It.Is<object[]>(args => (int)args[0] == 2 && (int)args[1] == 4),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [SkipOnCIFact]
    public async Task ApplicationChanged_BroadcastsFocusChangedEvent()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(100);

        var oldContext = new DesktopContext(0, 4, "Old Window", "old-class", "old-app", DateTime.UtcNow);
        var newContext = new DesktopContext(0, 4, "New Window", "new-class", "new-app", DateTime.UtcNow);
        var change = new DesktopContextChange(oldContext, newContext, ChangeType.ApplicationChanged);

        // Act
        _contextChangesSubject.OnNext(change);
        await Task.Delay(200);

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
        await Task.Delay(100);

        var oldContext = new DesktopContext(0, 4, "Old Title", "same-class", "same-app", DateTime.UtcNow);
        var newContext = new DesktopContext(0, 4, "New Title", "same-class", "same-app", DateTime.UtcNow);
        var change = new DesktopContextChange(oldContext, newContext, ChangeType.WindowFocusChanged);

        // Act
        _contextChangesSubject.OnNext(change);
        await Task.Delay(200);

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
        await Task.Delay(100);

        var oldContext = new DesktopContext(0, 4, "Old", "old", "old", DateTime.UtcNow);
        var newContext = new DesktopContext(1, 4, "New", "new", "new", DateTime.UtcNow);
        var change = new DesktopContextChange(oldContext, newContext, ChangeType.WorkspaceChanged);

        // Act
        _contextChangesSubject.OnNext(change);
        await Task.Delay(200);

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
        await Task.Delay(100);

        // Act
        await _sut.StopAsync(CancellationToken.None);

        // Push event after stop
        var context = new DesktopContext(0, 4, "Test", "test", "test", DateTime.UtcNow);
        var change = new DesktopContextChange(context, context, ChangeType.WorkspaceChanged);
        _contextChangesSubject.OnNext(change);
        await Task.Delay(200);

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

    public void Dispose()
    {
        _contextChangesSubject?.Dispose();
        _sut?.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
    }
}
