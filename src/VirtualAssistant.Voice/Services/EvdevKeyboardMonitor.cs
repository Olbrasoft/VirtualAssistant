using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Keyboard monitor using Linux evdev interface.
/// Reads raw keyboard events from /dev/input/eventX devices.
/// </summary>
public class EvdevKeyboardMonitor : IKeyboardMonitor
{
    private readonly ILogger<EvdevKeyboardMonitor> _logger;
    private readonly IKeyboardLedReader _ledReader;
    private readonly IReadOnlyList<string> _devicePaths;
    private readonly List<FileStream> _deviceStreams = [];
    private CancellationTokenSource? _cts;
    private readonly List<Task> _monitorTasks = [];
    private bool _disposed;

    // Linux input event structure size (struct input_event)
    // timeval (16 bytes on 64-bit) + type (2) + code (2) + value (4) = 24 bytes
    private const int InputEventSize = 24;
    private const ushort EV_KEY = 1;
    private const int KEY_PRESS = 1;
    private const int KEY_RELEASE = 0;

    public event EventHandler<KeyEventArgs>? KeyPressed;
    public event EventHandler<KeyEventArgs>? KeyReleased;

    public EvdevKeyboardMonitor(
        ILogger<EvdevKeyboardMonitor> logger,
        IKeyboardLedReader ledReader,
        IKeyboardDeviceDiscovery deviceDiscovery,
        string? devicePath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ledReader = ledReader ?? throw new ArgumentNullException(nameof(ledReader));
        ArgumentNullException.ThrowIfNull(deviceDiscovery);
        _devicePaths = devicePath is not null
            ? [devicePath]
            : deviceDiscovery.FindKeyboardDevices();
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_monitorTasks.Count > 0)
        {
            _logger.LogWarning("Keyboard monitor already running");
            return;
        }

        try
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var monitorToken = _cts.Token;

            foreach (var devicePath in _devicePaths)
            {
                try
                {
                    // Open in shared mode so desktop input handling is unaffected.
                    var stream = new FileStream(
                        devicePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite,
                        bufferSize: InputEventSize,
                        useAsync: false);

                    _deviceStreams.Add(stream);
                    _monitorTasks.Add(Task.Run(
                        () => MonitorLoop(devicePath, stream, monitorToken), monitorToken));
                    _logger.LogInformation("Keyboard monitor started on {Device}", devicePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to open keyboard device {Device}", devicePath);
                }
            }

            if (_monitorTasks.Count == 0)
                throw new InvalidOperationException("Failed to open any keyboard device");

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Stop();
            _logger.LogError(ex, "Failed to start keyboard monitor");
            throw;
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        _cts?.Cancel();
        foreach (var stream in _deviceStreams)
            stream.Dispose();
        _deviceStreams.Clear();
        _monitorTasks.Clear();
        _cts?.Dispose();
        _cts = null;
        _logger.LogInformation("Keyboard monitor stopped");
    }

    /// <inheritdoc />
    public bool IsScrollLockOn()
    {
        return _ledReader.IsScrollLockOn();
    }

    /// <inheritdoc />
    public bool IsCapsLockOn()
    {
        return _ledReader.IsCapsLockOn();
    }

    private void MonitorLoop(string devicePath, FileStream deviceStream, CancellationToken cancellationToken)
    {
        var buffer = new byte[InputEventSize];

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Synchronous blocking read for kernel events
                int bytesRead = deviceStream.Read(buffer, 0, InputEventSize);

                if (bytesRead == 0)
                    break;

                if (bytesRead < InputEventSize)
                    continue;

                // Parse input_event structure
                // Offset 16: type (ushort)
                // Offset 18: code (ushort)
                // Offset 20: value (int)
                ushort type = BitConverter.ToUInt16(buffer, 16);
                ushort code = BitConverter.ToUInt16(buffer, 18);
                int value = BitConverter.ToInt32(buffer, 20);

                if (type != EV_KEY)
                    continue;

                var keyCode = (KeyCode)code;

                // Only handle keys we care about
                if (keyCode != KeyCode.ScrollLock && keyCode != KeyCode.CapsLock && keyCode != KeyCode.Pause)
                    continue;

                var args = new KeyEventArgs
                {
                    Key = keyCode,
                    IsPressed = value == KEY_PRESS
                };

                if (value == KEY_PRESS)
                {
                    _logger.LogDebug("Key pressed: {Key}", keyCode);
                    KeyPressed?.Invoke(this, args);
                }
                else if (value == KEY_RELEASE)
                {
                    _logger.LogDebug("Key released: {Key}", keyCode);
                    KeyReleased?.Invoke(this, args);
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading keyboard event from {Device}", devicePath);
                break;
            }
        }

        _logger.LogInformation("Keyboard monitoring ended on {Device}", devicePath);
    }

    /// <summary>
    /// Releases resources used by the keyboard monitor, including stopping monitoring and closing device files.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        GC.SuppressFinalize(this);
    }
}
