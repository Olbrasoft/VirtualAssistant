using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.PushToTalk;

/// <summary>
/// Monitors USB Optical Mouse button events and simulates key presses.
/// Uses EVIOCGRAB to grab device exclusively - events won't propagate to system.
/// Designed for Logitech USB Optical Mouse used as a secondary push-to-talk trigger.
/// NOTE: Does NOT grab Logitech G203 LIGHTSYNC Gaming Mouse (main mouse).
/// </summary>
public class UsbMouseMonitor : IDisposable
{
    private readonly ILogger<UsbMouseMonitor> _logger;
    private readonly IKeyboardMonitor _keyboardMonitor;
    private readonly string _deviceNamePattern;

    private FileStream? _deviceStream;
    private bool _isMonitoring;
    private bool _disposed;
    private bool _deviceGrabbed;
    private Task? _reconnectTask;
    private CancellationTokenSource? _cts;

    // P/Invoke for ioctl to grab/ungrab the device
    [DllImport("libc", SetLastError = true)]
    private static extern int ioctl(int fd, uint request, int value);

    // EVIOCGRAB ioctl code (from linux/input.h)
    private const uint EVIOCGRAB = 0x40044590;

    // Linux input_event structure (24 bytes on 64-bit)
    private const int InputEventSize = 24;
    private const ushort EV_KEY = 0x01;
    private const ushort BTN_LEFT = 272;   // 0x110
    private const ushort BTN_RIGHT = 273;  // 0x111
    private const int KEY_PRESS = 1;
    private const int KEY_RELEASE = 0;

    // Reconnect settings
    private const int ReconnectIntervalMs = 2000;
    private const int MaxReconnectAttempts = int.MaxValue;

    // Double-click detection for LEFT button
    private const int DoubleClickThresholdMs = 400;
    private const int DoubleClickDebounceMs = 50;
    private DateTime _lastLeftClickTime = DateTime.MinValue;
    private CancellationTokenSource? _singleClickTimerCts;

    // Device to exclude (main mouse)
    private const string ExcludedDevice = "G203 LIGHTSYNC";

    /// <summary>
    /// Initializes a new instance of the <see cref="UsbMouseMonitor"/> class.
    /// </summary>
    public UsbMouseMonitor(
        ILogger<UsbMouseMonitor> logger,
        IKeyboardMonitor keyboardMonitor,
        string deviceNamePattern = "USB Optical Mouse")
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _keyboardMonitor = keyboardMonitor ?? throw new ArgumentNullException(nameof(keyboardMonitor));
        _deviceNamePattern = deviceNamePattern;
    }

    /// <summary>
    /// Event raised when a mouse button is pressed.
    /// </summary>
    public event EventHandler<MouseButtonEventArgs>? ButtonPressed;

    /// <summary>
    /// Event raised when a mouse button is released.
    /// </summary>
    public event EventHandler<MouseButtonEventArgs>? ButtonReleased;

    /// <summary>
    /// Gets a value indicating whether mouse monitoring is currently active.
    /// </summary>
    public bool IsMonitoring => _isMonitoring;

    /// <summary>
    /// Gets a value indicating whether the device is currently grabbed exclusively.
    /// </summary>
    public bool IsDeviceGrabbed => _deviceGrabbed;

    /// <summary>
    /// Starts monitoring USB mouse button events.
    /// </summary>
    public async Task StartMonitoringAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(UsbMouseMonitor));

        if (_isMonitoring)
        {
            _logger.LogWarning("USB mouse monitoring is already active");
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _isMonitoring = true;

        _reconnectTask = Task.Run(() => ReconnectLoopAsync(_cts.Token), _cts.Token);

        _logger.LogInformation("USB mouse monitoring started (looking for {Pattern})", _deviceNamePattern);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops monitoring USB mouse button events.
    /// </summary>
    public async Task StopMonitoringAsync()
    {
        if (!_isMonitoring)
        {
            _logger.LogWarning("USB mouse monitoring is not active");
            return;
        }

        try
        {
            _isMonitoring = false;
            _cts?.Cancel();

            if (_reconnectTask != null)
            {
                try
                {
                    await _reconnectTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }

            CloseDevice();

            _logger.LogInformation("USB mouse monitoring stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping USB mouse monitoring");
            throw;
        }
    }

    private async Task ReconnectLoopAsync(CancellationToken cancellationToken)
    {
        int attempts = 0;

        while (_isMonitoring && !cancellationToken.IsCancellationRequested && attempts < MaxReconnectAttempts)
        {
            try
            {
                var devicePath = FindUsbMouse();

                if (devicePath == null)
                {
                    if (attempts == 0 || attempts % 30 == 0)
                    {
                        _logger.LogInformation("USB mouse '{Pattern}' not found, waiting for connection...", _deviceNamePattern);
                    }
                    attempts++;
                    await Task.Delay(ReconnectIntervalMs, cancellationToken);
                    continue;
                }

                attempts = 0;

                if (await TryOpenDeviceAsync(devicePath, cancellationToken))
                {
                    _logger.LogInformation("Connected to USB mouse: {DevicePath}", devicePath);

                    await MonitorEventsAsync(cancellationToken);

                    _logger.LogWarning("USB mouse disconnected, will attempt reconnection...");
                    CloseDevice();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in USB mouse reconnect loop");
                CloseDevice();
                await Task.Delay(ReconnectIntervalMs, cancellationToken);
            }
        }
    }

    private async Task<bool> TryOpenDeviceAsync(string devicePath, CancellationToken cancellationToken)
    {
        try
        {
            _deviceStream = new FileStream(
                devicePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: InputEventSize,
                useAsync: false);

            int fd = (int)_deviceStream.SafeFileHandle.DangerousGetHandle();

            int result = ioctl(fd, EVIOCGRAB, 1);
            if (result == 0)
            {
                _deviceGrabbed = true;
                _logger.LogInformation("Device GRABBED exclusively - USB mouse events will NOT propagate to system");
            }
            else
            {
                int error = Marshal.GetLastWin32Error();
                _logger.LogWarning("Failed to grab device exclusively (error: {Error}). USB mouse events will propagate to system!", error);
                _deviceGrabbed = false;
            }

            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Permission denied. Add user to 'input' group: sudo usermod -a -G input $USER");
            return false;
        }
        catch (FileNotFoundException)
        {
            _logger.LogDebug("Device not found: {DevicePath}", devicePath);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open device: {DevicePath}", devicePath);
            return false;
        }
    }

    private void CloseDevice()
    {
        if (_deviceStream != null)
        {
            try
            {
                if (_deviceGrabbed)
                {
                    int fd = (int)_deviceStream.SafeFileHandle.DangerousGetHandle();
                    ioctl(fd, EVIOCGRAB, 0);
                    _deviceGrabbed = false;
                    _logger.LogDebug("Device ungrabbed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error ungrabbing device");
            }

            try
            {
                _deviceStream.Dispose();
            }
            catch { }

            _deviceStream = null;
        }
    }

    private async Task MonitorEventsAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[InputEventSize];

        try
        {
            while (_isMonitoring && !cancellationToken.IsCancellationRequested && _deviceStream != null)
            {
                int bytesRead;
                try
                {
                    bytesRead = _deviceStream.Read(buffer, 0, InputEventSize);
                }
                catch (IOException)
                {
                    _logger.LogDebug("IOException during read - device likely disconnected");
                    break;
                }

                if (bytesRead != InputEventSize)
                {
                    _logger.LogWarning("Incomplete event data received: {BytesRead} bytes", bytesRead);
                    break;
                }

                await ParseAndHandleEventAsync(buffer);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Event monitoring cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading USB mouse events");
        }
    }

    private async Task ParseAndHandleEventAsync(byte[] buffer)
    {
        int offset = 16;
        ushort type = BitConverter.ToUInt16(buffer, offset);
        offset += 2;
        ushort code = BitConverter.ToUInt16(buffer, offset);
        offset += 2;
        int value = BitConverter.ToInt32(buffer, offset);

        if (type != EV_KEY)
            return;

        if (value != KEY_PRESS && value != KEY_RELEASE)
            return;

        var button = code switch
        {
            BTN_LEFT => MouseButton.Left,
            BTN_RIGHT => MouseButton.Right,
            _ => MouseButton.Unknown
        };

        if (button == MouseButton.Unknown)
            return;

        var eventArgs = new MouseButtonEventArgs(button, code, value == KEY_PRESS, DateTime.UtcNow);

        if (value == KEY_PRESS)
        {
            _logger.LogDebug("USB mouse button pressed: {Button}", button);
            ButtonPressed?.Invoke(this, eventArgs);

            // LEFT button press - double-click detection
            if (button == MouseButton.Left)
            {
                var now = DateTime.UtcNow;
                var timeSinceLastClick = (now - _lastLeftClickTime).TotalMilliseconds;

                if (timeSinceLastClick <= DoubleClickThresholdMs && timeSinceLastClick > DoubleClickDebounceMs)
                {
                    _singleClickTimerCts?.Cancel();
                    _singleClickTimerCts = null;
                    _lastLeftClickTime = DateTime.MinValue;

                    _logger.LogInformation("USB LEFT DOUBLE-CLICK - simulating ESC (cancel transcription)");
                    try
                    {
                        await _keyboardMonitor.SimulateKeyPressAsync(KeyCode.Escape);
                        _keyboardMonitor.RaiseKeyReleasedEvent(KeyCode.Escape);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to simulate ESC on double-click");
                    }
                }
                else
                {
                    _lastLeftClickTime = now;
                    _singleClickTimerCts?.Cancel();
                    _singleClickTimerCts = new CancellationTokenSource();
                    var timerCts = _singleClickTimerCts;

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(DoubleClickThresholdMs, timerCts.Token);
                            _logger.LogInformation("USB LEFT SINGLE-CLICK - toggling CapsLock (start/stop recording)");
                            await _keyboardMonitor.SimulateKeyPressAsync(KeyCode.CapsLock);
                            await Task.Delay(100);
                            _keyboardMonitor.RaiseKeyReleasedEvent(KeyCode.CapsLock);
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.LogDebug("Single-click action cancelled (double-click detected)");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to toggle CapsLock on single-click");
                        }
                    });
                }
            }
            // RIGHT button press -> simulate Ctrl+C
            else if (button == MouseButton.Right)
            {
                _logger.LogInformation("USB RIGHT button pressed - simulating Ctrl+C (clear prompt)");
                try
                {
                    await _keyboardMonitor.SimulateKeyComboAsync(KeyCode.LeftControl, KeyCode.C);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to simulate Ctrl+C");
                }
            }
        }
        else
        {
            _logger.LogDebug("USB mouse button released: {Button}", button);
            ButtonReleased?.Invoke(this, eventArgs);
        }
    }

    private string? FindUsbMouse()
    {
        const string devicesPath = "/proc/bus/input/devices";
        if (!File.Exists(devicesPath))
            return null;

        try
        {
            var content = File.ReadAllText(devicesPath);
            var sections = content.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

            foreach (var section in sections)
            {
                // Skip excluded device (main mouse)
                if (section.Contains(ExcludedDevice, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!section.Contains(_deviceNamePattern, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!section.Contains("mouse", StringComparison.OrdinalIgnoreCase) &&
                    !section.Contains("Mouse", StringComparison.Ordinal))
                    continue;

                foreach (var line in section.Split('\n'))
                {
                    if (line.StartsWith("H: Handlers="))
                    {
                        var match = Regex.Match(line, @"event(\d+)");
                        if (match.Success)
                        {
                            var eventPath = $"/dev/input/event{match.Groups[1].Value}";
                            if (File.Exists(eventPath))
                            {
                                return eventPath;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing /proc/bus/input/devices");
        }

        return null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        if (_isMonitoring)
        {
            StopMonitoringAsync().GetAwaiter().GetResult();
        }

        _cts?.Dispose();
        CloseDevice();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
