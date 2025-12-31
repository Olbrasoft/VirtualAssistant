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
    private readonly IKeyboardDeviceDiscovery _deviceDiscovery;
    private readonly string _devicePath;
    private FileStream? _deviceStream;
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
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
        _deviceDiscovery = deviceDiscovery ?? throw new ArgumentNullException(nameof(deviceDiscovery));
        _devicePath = devicePath ?? _deviceDiscovery.FindKeyboardDevice();
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_monitorTask != null)
        {
            _logger.LogWarning("Keyboard monitor already running");
            return;
        }

        try
        {
            // Open device in shared mode (doesn't block X.org/Wayland)
            _deviceStream = new FileStream(
                _devicePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: InputEventSize,
                useAsync: false);

            _logger.LogInformation("Keyboard monitor started on {Device}", _devicePath);

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _monitorTask = Task.Run(() => MonitorLoop(_cts.Token), _cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open keyboard device {Device}", _devicePath);
            throw;
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        _cts?.Cancel();
        _deviceStream?.Dispose();
        _deviceStream = null;
        _monitorTask = null;
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

    private void MonitorLoop(CancellationToken cancellationToken)
    {
        var buffer = new byte[InputEventSize];

        while (!cancellationToken.IsCancellationRequested && _deviceStream != null)
        {
            try
            {
                // Synchronous blocking read for kernel events
                int bytesRead = _deviceStream.Read(buffer, 0, InputEventSize);

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
                _logger.LogError(ex, "Error reading keyboard event");
                break;
            }
        }
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
