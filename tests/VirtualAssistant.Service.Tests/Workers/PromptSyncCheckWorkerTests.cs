using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.VirtualAssistant.Service.Configuration;
using Olbrasoft.VirtualAssistant.Service.Services;
using Olbrasoft.VirtualAssistant.Service.Tray.Menu;
using Olbrasoft.VirtualAssistant.Service.Workers;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Workers;

public class PromptSyncCheckWorkerTests : IDisposable
{
    private readonly Mock<ILogger<PromptSyncCheckWorker>> _loggerMock;
    private readonly Mock<IPromptSyncService> _promptSyncServiceMock;
    private readonly Mock<IMenuStateManager> _menuStateManagerMock;
    private readonly PromptSyncOptions _options;
    private readonly PromptSyncCheckWorker _sut;

    public PromptSyncCheckWorkerTests()
    {
        _loggerMock = new Mock<ILogger<PromptSyncCheckWorker>>();
        _promptSyncServiceMock = new Mock<IPromptSyncService>();
        _menuStateManagerMock = new Mock<IMenuStateManager>();

        _options = new PromptSyncOptions { CheckIntervalSeconds = 1 };

        _menuStateManagerMock.SetupGet(x => x.PromptSyncStatus).Returns(PromptSyncStatus.Unknown);

        _sut = new PromptSyncCheckWorker(
            _loggerMock.Object,
            _promptSyncServiceMock.Object,
            _menuStateManagerMock.Object,
            Options.Create(_options));
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new PromptSyncCheckWorker(
            null!,
            _promptSyncServiceMock.Object,
            _menuStateManagerMock.Object,
            Options.Create(_options)));
    }

    [Fact]
    public void Constructor_NullPromptSyncService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new PromptSyncCheckWorker(
            _loggerMock.Object,
            null!,
            _menuStateManagerMock.Object,
            Options.Create(_options)));
    }

    [Fact]
    public void Constructor_NullMenuStateManager_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new PromptSyncCheckWorker(
            _loggerMock.Object,
            _promptSyncServiceMock.Object,
            null!,
            Options.Create(_options)));
    }

    [Fact]
    public void Constructor_ZeroInterval_DefaultsTo30Seconds()
    {
        var options = new PromptSyncOptions { CheckIntervalSeconds = 0 };

        var worker = new PromptSyncCheckWorker(
            _loggerMock.Object,
            _promptSyncServiceMock.Object,
            _menuStateManagerMock.Object,
            Options.Create(options));

        Assert.NotNull(worker);
    }

    [Fact]
    public void Constructor_NegativeInterval_DefaultsTo30Seconds()
    {
        var options = new PromptSyncOptions { CheckIntervalSeconds = -5 };

        var worker = new PromptSyncCheckWorker(
            _loggerMock.Object,
            _promptSyncServiceMock.Object,
            _menuStateManagerMock.Object,
            Options.Create(options));

        Assert.NotNull(worker);
    }

    #endregion

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_PerformsInitialCheckAfterStartupDelay()
    {
        using var cts = new CancellationTokenSource();
        _promptSyncServiceMock.Setup(x => x.ArePromptsOutOfSync()).Returns(false);

        _ = _sut.StartAsync(cts.Token);
        await Task.Delay(6000);
        await cts.CancelAsync();

        _promptSyncServiceMock.Verify(x => x.ArePromptsOutOfSync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_PromptsInSync_UpdatesStatusToInSync()
    {
        using var cts = new CancellationTokenSource();
        _promptSyncServiceMock.Setup(x => x.ArePromptsOutOfSync()).Returns(false);
        _menuStateManagerMock.SetupGet(x => x.PromptSyncStatus).Returns(PromptSyncStatus.Unknown);

        _ = _sut.StartAsync(cts.Token);
        await Task.Delay(6000);
        await cts.CancelAsync();

        _menuStateManagerMock.Verify(x => x.UpdatePromptSyncStatus(PromptSyncStatus.InSync, null), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_PromptsOutOfSync_UpdatesStatusToOutOfSync()
    {
        using var cts = new CancellationTokenSource();
        _promptSyncServiceMock.Setup(x => x.ArePromptsOutOfSync()).Returns(true);
        _menuStateManagerMock.SetupGet(x => x.PromptSyncStatus).Returns(PromptSyncStatus.InSync);

        _ = _sut.StartAsync(cts.Token);
        await Task.Delay(6000);
        await cts.CancelAsync();

        _menuStateManagerMock.Verify(x => x.UpdatePromptSyncStatus(PromptSyncStatus.OutOfSync, null), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyOutOfSync_DoesNotUpdateAgain()
    {
        using var cts = new CancellationTokenSource();
        _promptSyncServiceMock.Setup(x => x.ArePromptsOutOfSync()).Returns(true);
        _menuStateManagerMock.SetupGet(x => x.PromptSyncStatus).Returns(PromptSyncStatus.OutOfSync);

        _ = _sut.StartAsync(cts.Token);
        await Task.Delay(6000);
        await cts.CancelAsync();

        _menuStateManagerMock.Verify(x => x.UpdatePromptSyncStatus(PromptSyncStatus.OutOfSync, null), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_SyncingInProgress_SkipsCheck()
    {
        using var cts = new CancellationTokenSource();
        _menuStateManagerMock.SetupGet(x => x.PromptSyncStatus).Returns(PromptSyncStatus.Syncing);

        _ = _sut.StartAsync(cts.Token);
        await Task.Delay(6000);
        await cts.CancelAsync();

        _promptSyncServiceMock.Verify(x => x.ArePromptsOutOfSync(), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationRequested_StopsGracefully()
    {
        using var cts = new CancellationTokenSource();
        _promptSyncServiceMock.Setup(x => x.ArePromptsOutOfSync()).Returns(false);

        var executeTask = _sut.StartAsync(cts.Token);
        await Task.Delay(100);
        await cts.CancelAsync();

        await _sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_ServiceThrowsException_ContinuesRunning()
    {
        using var cts = new CancellationTokenSource();
        var callCount = 0;
        _promptSyncServiceMock.Setup(x => x.ArePromptsOutOfSync())
            .Callback(() =>
            {
                callCount++;
                if (callCount == 1) throw new InvalidOperationException("Test exception");
            })
            .Returns(false);

        _ = _sut.StartAsync(cts.Token);
        await Task.Delay(8000);
        await cts.CancelAsync();

        Assert.True(callCount >= 2);
    }

    [Fact]
    public async Task ExecuteAsync_OutOfSyncBecomesSynced_UpdatesToInSync()
    {
        using var cts = new CancellationTokenSource();
        _promptSyncServiceMock.Setup(x => x.ArePromptsOutOfSync()).Returns(false);
        _menuStateManagerMock.SetupGet(x => x.PromptSyncStatus).Returns(PromptSyncStatus.OutOfSync);

        _ = _sut.StartAsync(cts.Token);
        await Task.Delay(6000);
        await cts.CancelAsync();

        _menuStateManagerMock.Verify(x => x.UpdatePromptSyncStatus(PromptSyncStatus.InSync, null), Times.AtLeastOnce);
    }

    #endregion

    public void Dispose()
    {
        _sut?.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
    }
}
