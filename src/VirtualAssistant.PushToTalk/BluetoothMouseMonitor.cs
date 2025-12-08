using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.PushToTalk;

/// <summary>
/// Monitors Bluetooth mouse button events and simulates CapsLock key press.
/// Uses EVIOCGRAB to grab device exclusively - events won't propagate to system.
/// Designed for Microsoft BluetoothMouse3600 used as a remote push-to-talk trigger.
/// </summary>
public class BluetoothMouseMonitor : IDisposable
{
    private readonly ILogger<BluetoothMouseMonitor> _logger;
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
    // _IOW('E', 0x90, int) = 0x40044590
    private const uint EVIOCGRAB = 0x40044590;

    // Linux input_event structure (24 bytes on 64-bit)
    private const int InputEventSize = 24;
    private const ushort EV_KEY = 0x01;
    private const ushort BTN_LEFT = 272;   // 0x110
    private const ushort BTN_RIGHT = 273;  // 0x111
    private const ushort BTN_MIDDLE = 274; // 0x112
    private const int KEY_PRESS = 1;
    private const int KEY_RELEASE = 0;

    // Reconnect settings
    private const int ReconnectIntervalMs = 2000;
    private const int MaxReconnectAttempts = int.MaxValue; // Keep trying forever

    // Double-click detection for LEFT and RIGHT buttons
    private const int DoubleClickThresholdMs = 400;  // Max time between clicks for double-click
    private const int DoubleClickDebounceMs = 50;    // Min time between clicks (debounce)
    private DateTime _lastLeftClickTime = DateTime.MinValue;
    private DateTime _lastRightClickTime = DateTime.MinValue;
    private CancellationTokenSource? _singleClickTimerCts;

    /// <summary>
    /// Initializes a new instance of the <see cref="BluetoothMouseMonitor"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="keyboardMonitor">Keyboard monitor for simulating CapsLock key press.</param>
    /// <param name="deviceNamePattern">Device name pattern to search for (default: "BluetoothMouse3600").</param>
    public BluetoothMouseMonitor(
        ILogger<BluetoothMouseMonitor> logger,
        IKeyboardMonitor keyboardMonitor,
        string deviceNamePattern = "BluetoothMouse3600")
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
    /// Starts monitoring Bluetooth mouse button events.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StartMonitoringAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(BluetoothMouseMonitor));

        if (_isMonitoring)
        {
            _logger.LogWarning("Bluetooth mouse monitoring is already active");
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _isMonitoring = true;

        // Start the reconnect task that handles initial connection and reconnection
        _reconnectTask = Task.Run(() => ReconnectLoopAsync(_cts.Token), _cts.Token);

        _logger.LogInformation("Bluetooth mouse monitoring started (looking for {Pattern})", _deviceNamePattern);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops monitoring Bluetooth mouse button events.
    /// </summary>
    public async Task StopMonitoringAsync()
    {
        if (!_isMonitoring)
        {
            _logger.LogWarning("Bluetooth mouse monitoring is not active");
            return;
        }

        try
        {
            _isMonitoring = false;
            _cts?.Cancel();

            // Wait for reconnect task to complete
            if (_reconnectTask != null)
            {
                try
                {
                    await _reconnectTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                }
            }

            CloseDevice();

            _logger.LogInformation("Bluetooth mouse monitoring stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping Bluetooth mouse monitoring");
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
                var devicePath = FindBluetoothMouse();

                if (devicePath == null)
                {
                    if (attempts == 0 || attempts % 30 == 0) // Log every minute (30 * 2s)
                    {
                        _logger.LogInformation("Bluetooth mouse '{Pattern}' not found, waiting for connection...", _deviceNamePattern);
                    }
                    attempts++;
                    await Task.Delay(ReconnectIntervalMs, cancellationToken);
                    continue;
                }

                attempts = 0; // Reset attempts on successful find

                if (await TryOpenDeviceAsync(devicePath, cancellationToken))
                {
                    _logger.LogInformation("Connected to Bluetooth mouse: {DevicePath}", devicePath);

                    // Monitor until disconnection or cancellation
                    await MonitorEventsAsync(cancellationToken);

                    _logger.LogWarning("Bluetooth mouse disconnected, will attempt reconnection...");
                    CloseDevice();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Bluetooth mouse reconnect loop");
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

            // Get the file descriptor for ioctl
            int fd = (int)_deviceStream.SafeFileHandle.DangerousGetHandle();

            // GRAB the device exclusively - this prevents events from propagating to system!
            int result = ioctl(fd, EVIOCGRAB, 1);
            if (result == 0)
            {
                _deviceGrabbed = true;
                _logger.LogInformation("Device GRABBED exclusively - mouse events will NOT propagate to system");
            }
            else
            {
                int error = Marshal.GetLastWin32Error();
                _logger.LogWarning("Failed to grab device exclusively (error: {Error}). Mouse events will propagate to system!", error);
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
                    ioctl(fd, EVIOCGRAB, 0); // Ungrab
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
                    // Device disconnected
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
            _logger.LogError(ex, "Error reading mouse events");
        }
    }

    private async Task ParseAndHandleEventAsync(byte[] buffer)
    {
        // Skip timeval (first 16 bytes)
        int offset = 16;

        // Read type (2 bytes)
        ushort type = BitConverter.ToUInt16(buffer, offset);
        offset += 2;

        // Read code (2 bytes) - button code
        ushort code = BitConverter.ToUInt16(buffer, offset);
        offset += 2;

        // Read value (4 bytes) - 0=release, 1=press
        int value = BitConverter.ToInt32(buffer, offset);

        // Only process button events
        if (type != EV_KEY)
            return;

        // Only process press and release (not repeat)
        if (value != KEY_PRESS && value != KEY_RELEASE)
            return;

        var button = code switch
        {
            BTN_LEFT => MouseButton.Left,
            BTN_RIGHT => MouseButton.Right,
            BTN_MIDDLE => MouseButton.Middle,
            _ => MouseButton.Unknown
        };

        if (button == MouseButton.Unknown)
            return;

        var eventArgs = new MouseButtonEventArgs(button, code, value == KEY_PRESS, DateTime.UtcNow);

        if (value == KEY_PRESS)
        {
            _logger.LogDebug("Mouse button pressed: {Button}", button);
            ButtonPressed?.Invoke(this, eventArgs);

            // LEFT button press - double-click detection
            // Single click → CapsLock (toggle recording)
            // Double click → ESC (cancel transcription)
            if (button == MouseButton.Left)
            {
                var now = DateTime.UtcNow;
                var timeSinceLastClick = (now - _lastLeftClickTime).TotalMilliseconds;

                // Check for double-click (within threshold and after debounce)
                if (timeSinceLastClick <= DoubleClickThresholdMs && timeSinceLastClick > DoubleClickDebounceMs)
                {
                    // Double-click detected - cancel any pending single-click action
                    _singleClickTimerCts?.Cancel();
                    _singleClickTimerCts = null;
                    _lastLeftClickTime = DateTime.MinValue; // Reset to prevent triple-click

                    _logger.LogInformation("LEFT DOUBLE-CLICK - simulating ESC (cancel transcription)");
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
                    // First click - schedule single-click action after delay
                    _lastLeftClickTime = now;

                    // Cancel any previous pending single-click
                    _singleClickTimerCts?.Cancel();
                    _singleClickTimerCts = new CancellationTokenSource();
                    var timerCts = _singleClickTimerCts;

                    // Schedule single-click action (CapsLock toggle) after double-click threshold
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(DoubleClickThresholdMs, timerCts.Token);

                            // If we get here, no second click happened - execute single-click action
                            _logger.LogInformation("LEFT SINGLE-CLICK - toggling CapsLock (start/stop recording)");

                            // Simulate key press to toggle LED state
                            await _keyboardMonitor.SimulateKeyPressAsync(KeyCode.CapsLock);
                            await Task.Delay(100);
                            _keyboardMonitor.RaiseKeyReleasedEvent(KeyCode.CapsLock);
                        }
                        catch (OperationCanceledException)
                        {
                            // Cancelled by double-click - this is expected
                            _logger.LogDebug("Single-click action cancelled (double-click detected)");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to toggle CapsLock on single-click");
                        }
                    });
                }
            }
            // RIGHT button press - double-click detection (safety for destructive Ctrl+C)
            // Single click → nothing (just record time)
            // Double click → Ctrl+C (clear prompt)
            else if (button == MouseButton.Right)
            {
                var now = DateTime.UtcNow;
                var timeSinceLastClick = (now - _lastRightClickTime).TotalMilliseconds;

                // Check for double-click (within threshold and after debounce)
                if (timeSinceLastClick <= DoubleClickThresholdMs && timeSinceLastClick > DoubleClickDebounceMs)
                {
                    // Double-click detected - execute Ctrl+C
                    _lastRightClickTime = DateTime.MinValue; // Reset to prevent triple-click

                    _logger.LogInformation("RIGHT DOUBLE-CLICK - simulating Ctrl+C (clear prompt)");
                    try
                    {
                        await _keyboardMonitor.SimulateKeyComboAsync(KeyCode.LeftControl, KeyCode.C);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to simulate Ctrl+C on double-click");
                    }
                }
                else
                {
                    // Single click - just record time, do nothing else
                    _lastRightClickTime = now;
                    _logger.LogDebug("RIGHT single-click recorded (double-click within {Threshold}ms for Ctrl+C)", DoubleClickThresholdMs);
                }
            }
            // MIDDLE button press -> simulate Enter key (confirm/send in active window)
            else if (button == MouseButton.Middle)
            {
                _logger.LogInformation("MIDDLE button pressed - simulating Enter key press");
                try
                {
                    // Simulate physical Enter key press via uinput
                    // This sends Enter to the active window (e.g., to send message in chat)
                    await _keyboardMonitor.SimulateKeyPressAsync(KeyCode.Enter);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to simulate Enter key press");
                }
            }
        }
        else
        {
            _logger.LogDebug("Mouse button released: {Button}", button);
            ButtonReleased?.Invoke(this, eventArgs);
        }
    }

    private string? FindBluetoothMouse()
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
                // Check if this device matches our pattern
                if (!section.Contains(_deviceNamePattern, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Must be a mouse (have "mouse" in handlers or name)
                if (!section.Contains("mouse", StringComparison.OrdinalIgnoreCase) &&
                    !section.Contains("Mouse", StringComparison.Ordinal))
                    continue;

                // Find the event handler
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

/// <summary>
/// Mouse button enumeration.
/// </summary>
public enum MouseButton
{
    Unknown = 0,
    Left = 272,    // BTN_LEFT
    Right = 273,   // BTN_RIGHT
    Middle = 274   // BTN_MIDDLE
}

/// <summary>
/// Event arguments for mouse button events.
/// </summary>
public class MouseButtonEventArgs : EventArgs
{
    public MouseButtonEventArgs(MouseButton button, ushort rawCode, bool isPressed, DateTime timestamp)
    {
        Button = button;
        RawCode = rawCode;
        IsPressed = isPressed;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Gets the mouse button.
    /// </summary>
    public MouseButton Button { get; }

    /// <summary>
    /// Gets the raw button code from evdev.
    /// </summary>
    public ushort RawCode { get; }

    /// <summary>
    /// Gets whether the button is pressed (true) or released (false).
    /// </summary>
    public bool IsPressed { get; }

    /// <summary>
    /// Gets the timestamp of the event.
    /// </summary>
    public DateTime Timestamp { get; }
}
