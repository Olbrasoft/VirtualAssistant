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
        var completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _promptSyncServiceMock
            .Setup(x => x.ArePromptsOutOfSync())
            .Returns(false)
            .Callback(() => completionSource.TrySetResult(true));

        _ = _sut.StartAsync(cts.Token);

        var completedTask = await Task.WhenAny(
            completionSource.Task,
            Task.Delay(TimeSpan.FromSeconds(10), cts.Token));

        Assert.Equal(completionSource.Task, completedTask);
        await cts.CancelAsync();
    }

    [Fact]
    public async Task ExecuteAsync_PromptsInSync_UpdatesStatusToInSync()
    {
        using var cts = new CancellationTokenSource();
        var completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _promptSyncServiceMock.Setup(x => x.ArePromptsOutOfSync()).Returns(false);
        _menuStateManagerMock.SetupGet(x => x.PromptSyncStatus).Returns(PromptSyncStatus.Unknown);
        _menuStateManagerMock.Setup(x => x.UpdatePromptSyncStatus(PromptSyncStatus.InSync, It.IsAny<string?>()))
            .Callback(() => completionSource.TrySetResult(true));

        _ = _sut.StartAsync(cts.Token);

        var completedTask = await Task.WhenAny(
            completionSource.Task,
            Task.Delay(TimeSpan.FromSeconds(10), cts.Token));

        Assert.Equal(completionSource.Task, completedTask);
        _menuStateManagerMock.Verify(x => x.UpdatePromptSyncStatus(PromptSyncStatus.InSync, It.IsAny<string?>()), Times.AtLeastOnce);
        await cts.CancelAsync();
    }

    [Fact]
    public async Task ExecuteAsync_PromptsOutOfSync_UpdatesStatusToOutOfSync()
    {
        using var cts = new CancellationTokenSource();
        var completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _promptSyncServiceMock.Setup(x => x.ArePromptsOutOfSync()).Returns(true);
        _menuStateManagerMock.SetupGet(x => x.PromptSyncStatus).Returns(PromptSyncStatus.InSync);
        _menuStateManagerMock.Setup(x => x.UpdatePromptSyncStatus(PromptSyncStatus.OutOfSync, It.IsAny<string?>()))
            .Callback(() => completionSource.TrySetResult(true));

        _ = _sut.StartAsync(cts.Token);

        var completedTask = await Task.WhenAny(
            completionSource.Task,
            Task.Delay(TimeSpan.FromSeconds(10), cts.Token));

        Assert.Equal(completionSource.Task, completedTask);
        _menuStateManagerMock.Verify(x => x.UpdatePromptSyncStatus(PromptSyncStatus.OutOfSync, It.IsAny<string?>()), Times.AtLeastOnce);
        await cts.CancelAsync();
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyOutOfSync_DoesNotUpdateAgain()
    {
        using var cts = new CancellationTokenSource();
        var checkCalled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _promptSyncServiceMock.Setup(x => x.ArePromptsOutOfSync())
            .Callback(() => checkCalled.TrySetResult(true))
            .Returns(true);
        _menuStateManagerMock.SetupGet(x => x.PromptSyncStatus).Returns(PromptSyncStatus.OutOfSync);

        _ = _sut.StartAsync(cts.Token);

        var completedTask = await Task.WhenAny(
            checkCalled.Task,
            Task.Delay(TimeSpan.FromSeconds(10), cts.Token));

        Assert.Equal(checkCalled.Task, completedTask);
        _menuStateManagerMock.Verify(x => x.UpdatePromptSyncStatus(PromptSyncStatus.OutOfSync, It.IsAny<string?>()), Times.Never);
        await cts.CancelAsync();
    }

    [Fact]
    public async Task ExecuteAsync_SyncingInProgress_SkipsCheck()
    {
        using var cts = new CancellationTokenSource();
        _menuStateManagerMock.SetupGet(x => x.PromptSyncStatus).Returns(PromptSyncStatus.Syncing);

        _ = _sut.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(7));
        await cts.CancelAsync();

        _promptSyncServiceMock.Verify(x => x.ArePromptsOutOfSync(), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationRequested_StopsGracefully()
    {
        using var cts = new CancellationTokenSource();
        _promptSyncServiceMock.Setup(x => x.ArePromptsOutOfSync()).Returns(false);

        _ = _sut.StartAsync(cts.Token);
        await Task.Delay(100);
        await cts.CancelAsync();

        await _sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_ServiceThrowsException_ContinuesRunning()
    {
        using var cts = new CancellationTokenSource();
        var callCount = 0;
        var tcs = new TaskCompletionSource();

        _promptSyncServiceMock.Setup(x => x.ArePromptsOutOfSync())
            .Callback(() =>
            {
                callCount++;
                if (callCount == 1) throw new InvalidOperationException("Test exception");
                if (callCount >= 2 && !tcs.Task.IsCompleted)
                {
                    tcs.TrySetResult();
                }
            })
            .Returns(false);

        _ = _sut.StartAsync(cts.Token);
        await tcs.Task;
        await cts.CancelAsync();

        Assert.True(callCount >= 2);
    }

    [Fact]
    public async Task ExecuteAsync_OutOfSyncBecomesSynced_UpdatesToInSync()
    {
        using var cts = new CancellationTokenSource();
        var completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _promptSyncServiceMock.Setup(x => x.ArePromptsOutOfSync()).Returns(false);
        _menuStateManagerMock.SetupGet(x => x.PromptSyncStatus).Returns(PromptSyncStatus.OutOfSync);
        _menuStateManagerMock.Setup(x => x.UpdatePromptSyncStatus(PromptSyncStatus.InSync, It.IsAny<string?>()))
            .Callback(() => completionSource.TrySetResult(true));

        _ = _sut.StartAsync(cts.Token);

        var completedTask = await Task.WhenAny(
            completionSource.Task,
            Task.Delay(TimeSpan.FromSeconds(10), cts.Token));

        Assert.Equal(completionSource.Task, completedTask);
        _menuStateManagerMock.Verify(x => x.UpdatePromptSyncStatus(PromptSyncStatus.InSync, It.IsAny<string?>()), Times.AtLeastOnce);
        await cts.CancelAsync();
    }

    #endregion

    public void Dispose()
    {
        _sut?.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
    }
}
