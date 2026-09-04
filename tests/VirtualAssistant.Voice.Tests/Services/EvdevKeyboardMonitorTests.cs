using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Tests.Services;

public class EvdevKeyboardMonitorTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];

    [Fact]
    public async Task StartAsync_MultipleKeyboards_RaisesEventsFromEveryDevice()
    {
        // Arrange
        var firstDevice = CreateDeviceFile(KeyCode.ScrollLock);
        var secondDevice = CreateDeviceFile(KeyCode.Pause);
        var discovery = new Mock<IKeyboardDeviceDiscovery>();
        discovery.Setup(x => x.FindKeyboardDevices()).Returns([firstDevice, secondDevice]);
        var monitor = new EvdevKeyboardMonitor(
            Mock.Of<ILogger<EvdevKeyboardMonitor>>(),
            Mock.Of<IKeyboardLedReader>(),
            discovery.Object);
        var releasedKeys = new List<KeyCode>();
        var eventsReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        monitor.KeyReleased += (_, args) =>
        {
            lock (releasedKeys)
            {
                releasedKeys.Add(args.Key);
                if (releasedKeys.Count == 2)
                    eventsReceived.TrySetResult();
            }
        };

        // Act
        await monitor.StartAsync(CancellationToken.None);
        await eventsReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));
        monitor.Dispose();

        // Assert
        Assert.Contains(KeyCode.ScrollLock, releasedKeys);
        Assert.Contains(KeyCode.Pause, releasedKeys);
    }

    [Fact]
    public async Task StartAsync_ExplicitDevice_MonitorsOnlyConfiguredDevice()
    {
        // Arrange
        var configuredDevice = CreateDeviceFile(KeyCode.ScrollLock);
        var ignoredDevice = CreateDeviceFile(KeyCode.Pause);
        var discovery = new Mock<IKeyboardDeviceDiscovery>(MockBehavior.Strict);
        var monitor = new EvdevKeyboardMonitor(
            Mock.Of<ILogger<EvdevKeyboardMonitor>>(),
            Mock.Of<IKeyboardLedReader>(),
            discovery.Object,
            configuredDevice);
        var released = new TaskCompletionSource<KeyCode>(TaskCreationOptions.RunContinuationsAsynchronously);
        monitor.KeyReleased += (_, args) => released.TrySetResult(args.Key);

        // Act
        await monitor.StartAsync(CancellationToken.None);
        var key = await released.Task.WaitAsync(TimeSpan.FromSeconds(2));
        monitor.Dispose();

        // Assert
        Assert.Equal(KeyCode.ScrollLock, key);
        discovery.Verify(x => x.FindKeyboardDevices(), Times.Never);
        Assert.True(File.Exists(ignoredDevice));
    }

    private string CreateDeviceFile(KeyCode key)
    {
        var path = Path.Combine(Path.GetTempPath(), $"evdev-{Guid.NewGuid():N}");
        var inputEvent = new byte[24];
        BitConverter.GetBytes((ushort)1).CopyTo(inputEvent, 16);
        BitConverter.GetBytes((ushort)key).CopyTo(inputEvent, 18);
        BitConverter.GetBytes(0).CopyTo(inputEvent, 20);
        File.WriteAllBytes(path, inputEvent);
        _temporaryFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var path in _temporaryFiles)
            File.Delete(path);
    }
}
