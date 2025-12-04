using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.PushToTalk;

/// <summary>
/// Linux evdev-based keyboard monitor.
/// Reads keyboard events directly from /dev/input/eventX devices.
/// </summary>
public class EvdevKeyboardMonitor : IKeyboardMonitor
{
    private readonly ILogger<EvdevKeyboardMonitor> _logger;
    private readonly string _devicePath;
    private FileStream? _deviceStream;
    private bool _isMonitoring;
    private bool _disposed;
    private Task? _monitorTask;
    private CancellationTokenSource? _cts;

    // Linux input_event structure (24 bytes)
    // struct input_event {
    //     struct timeval time;  // 16 bytes (tv_sec: 8, tv_usec: 8)
    //     __u16 type;           // 2 bytes
    //     __u16 code;           // 2 bytes
    //     __s32 value;          // 4 bytes
    // };
    private const int InputEventSize = 24;
    private const ushort EV_KEY = 1;  // Key press/release event type
    private const int KEY_PRESS = 1;
    private const int KEY_RELEASE = 0;



    /// <summary>
    /// Initializes a new instance of the <see cref="EvdevKeyboardMonitor"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="devicePath">Path to input device (default: auto-detect keyboard).</param>
    public EvdevKeyboardMonitor(ILogger<EvdevKeyboardMonitor> logger, string? devicePath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _devicePath = devicePath ?? FindKeyboardDevice();
    }

    /// <inheritdoc/>
    public event EventHandler<KeyEventArgs>? KeyPressed;

    /// <inheritdoc/>
    public event EventHandler<KeyEventArgs>? KeyReleased;

    /// <inheritdoc/>
    public bool IsMonitoring => _isMonitoring;

    /// <inheritdoc/>
    public async Task StartMonitoringAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EvdevKeyboardMonitor));

        if (_isMonitoring)
        {
            _logger.LogWarning("Keyboard monitoring is already active");
            return;
        }

        try
        {
            _logger.LogInformation("Opening keyboard device: {DevicePath}", _devicePath);

            // Open device in read-only mode (synchronous for device files)
            _deviceStream = new FileStream(
                _devicePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 24,
                useAsync: false);

            // NOTE: EVIOCGRAB is NOT used because it would block ALL keys on the device.
            // We read events in shared mode - both our app and X.org receive events.
            // This is fine for ScrollLock/CapsLock monitoring.
            _logger.LogInformation("Reading keyboard events in shared mode (X.org also receives events)");

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _isMonitoring = true;

            _logger.LogInformation("Keyboard monitoring started");

            // Start monitoring in background
            _monitorTask = Task.Run(() => MonitorEventsAsync(_cts.Token), _cts.Token);

            // Don't await - let it run in background
            // await _monitorTask;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Permission denied. Add user to 'input' group: sudo usermod -a -G input $USER");
            _isMonitoring = false;
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Keyboard monitoring was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start keyboard monitoring");
            _isMonitoring = false;
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task StopMonitoringAsync()
    {
        if (!_isMonitoring)
        {
            _logger.LogWarning("Keyboard monitoring is not active");
            return;
        }

        try
        {
            _isMonitoring = false;
            _cts?.Cancel();

            if (_monitorTask != null)
            {
                await _monitorTask;
            }

            if (_deviceStream != null)
            {
                await _deviceStream.DisposeAsync();
                _deviceStream = null;
            }

            _logger.LogInformation("Keyboard monitoring stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping keyboard monitoring");
            throw;
        }
    }

    private Task MonitorEventsAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[InputEventSize];

        try
        {
            _logger.LogInformation("Starting event monitoring loop (synchronous read)");
            
            while (_isMonitoring && !cancellationToken.IsCancellationRequested)
            {
                // Use synchronous Read for device files (blocking read)
                // This properly waits for kernel events
                int bytesRead = _deviceStream!.Read(buffer, 0, InputEventSize);

                if (bytesRead != InputEventSize)
                {
                    _logger.LogWarning("Incomplete event data received: {BytesRead} bytes", bytesRead);
                    continue;
                }

                // Parse input_event structure
                ParseInputEvent(buffer);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
            _logger.LogInformation("Event monitoring cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading keyboard events");
        }
        
        _logger.LogInformation("Event monitoring loop ended");
        return Task.CompletedTask;
    }

    private void ParseInputEvent(byte[] buffer)
    {
        // Skip timeval (first 16 bytes)
        int offset = 16;

        // Read type (2 bytes)
        ushort type = BitConverter.ToUInt16(buffer, offset);
        offset += 2;

        // Read code (2 bytes) - this is the key code
        ushort code = BitConverter.ToUInt16(buffer, offset);
        offset += 2;

        // Read value (4 bytes) - 0=release, 1=press, 2=repeat
        int value = BitConverter.ToInt32(buffer, offset);

        // Only process key events
        if (type != EV_KEY)
            return;

        // Ignore key repeat events
        if (value != KEY_PRESS && value != KEY_RELEASE)
            return;

        // Convert code to KeyCode enum
        var keyCode = Enum.IsDefined(typeof(KeyCode), (int)code)
            ? (KeyCode)code
            : KeyCode.Unknown;

        var eventArgs = new KeyEventArgs(keyCode, code, DateTime.UtcNow);

        if (value == KEY_PRESS)
        {
            _logger.LogDebug("Key pressed: {KeyCode} (raw code: {RawCode})", keyCode, code);
            KeyPressed?.Invoke(this, eventArgs);
        }
        else if (value == KEY_RELEASE)
        {
            _logger.LogDebug("Key released: {KeyCode} (raw code: {RawCode})", keyCode, code);
            KeyReleased?.Invoke(this, eventArgs);
        }
    }

    private static string FindKeyboardDevice()
    {
        // First, try to find a real keyboard (one with LED indicators like CapsLock)
        // by checking /proc/bus/input/devices for devices with "leds" handler
        var realKeyboard = FindRealKeyboardFromProcDevices();
        if (realKeyboard != null)
        {
            return realKeyboard;
        }

        // Fallback: Try to find keyboard device in /dev/input/by-path/
        // Prefer devices that don't contain "mouse" in their path
        var byPathDir = "/dev/input/by-path";
        if (Directory.Exists(byPathDir))
        {
            var kbdDevices = Directory.GetFiles(byPathDir, "*kbd")
                .Where(f => !f.Contains("mouse", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f) // Consistent ordering
                .ToArray();
            
            if (kbdDevices.Length > 0)
            {
                return kbdDevices[0];
            }
        }

        // Last fallback: try common event devices
        for (int i = 0; i < 30; i++)
        {
            var devicePath = $"/dev/input/event{i}";
            if (File.Exists(devicePath))
            {
                return devicePath;
            }
        }

        throw new FileNotFoundException("No keyboard input device found. Check /dev/input/");
    }

    /// <summary>
    /// Finds a real keyboard device by parsing /proc/bus/input/devices.
    /// Real keyboards have "leds" in their Handlers line (for CapsLock/NumLock LEDs).
    /// </summary>
    private static string? FindRealKeyboardFromProcDevices()
    {
        const string devicesPath = "/proc/bus/input/devices";
        if (!File.Exists(devicesPath))
            return null;

        try
        {
            var content = File.ReadAllText(devicesPath);
            var devices = content.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

            foreach (var device in devices)
            {
                var lines = device.Split('\n');
                string? name = null;
                string? handlers = null;

                foreach (var line in lines)
                {
                    if (line.StartsWith("N: Name="))
                        name = line;
                    if (line.StartsWith("H: Handlers="))
                        handlers = line;
                }

                // Look for keyboard with LED support (real keyboards have "leds" handler)
                // Skip devices that are mice or have "Mouse" in name
                if (handlers != null && 
                    handlers.Contains("kbd") && 
                    handlers.Contains("leds") &&
                    (name == null || !name.Contains("Mouse", StringComparison.OrdinalIgnoreCase)))
                {
                    // Extract event number from handlers line
                    // Format: H: Handlers=sysrq kbd leds event17
                    var match = Regex.Match(handlers, @"event(\d+)");
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
        catch
        {
            // Ignore errors, fall through to other methods
        }

        return null;
    }

    /// <inheritdoc/>
    public bool IsCapsLockOn()
    {
        return ReadLedState("capslock");
    }

    /// <inheritdoc/>
    public bool IsScrollLockOn()
    {
        return ReadLedState("scrolllock");
    }

    /// <summary>
    /// Reads the LED state from /sys/class/leds/ for the specified LED name.
    /// </summary>
    private bool ReadLedState(string ledName)
    {
        try
        {
            var ledsDir = "/sys/class/leds";
            if (Directory.Exists(ledsDir))
            {
                var led = Directory.GetDirectories(ledsDir)
                    .FirstOrDefault(d => d.Contains(ledName, StringComparison.OrdinalIgnoreCase));

                if (led != null)
                {
                    var brightnessPath = Path.Combine(led, "brightness");
                    if (File.Exists(brightnessPath))
                    {
                        var value = File.ReadAllText(brightnessPath).Trim();
                        var isOn = value != "0";
                        _logger.LogDebug("{LedName} LED state: {State} (brightness: {Value})", ledName, isOn ? "ON" : "OFF", value);
                        return isOn;
                    }
                }
            }

            _logger.LogWarning("Could not find {LedName} LED in /sys/class/leds/", ledName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading {LedName} state", ledName);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task SimulateKeyPressAsync(KeyCode key)
    {
        try
        {
            _logger.LogInformation("Simulating key press: {Key}", key);
            
            // Use uinput to simulate key press
            // We need to create a virtual input device
            const string uinputPath = "/dev/uinput";
            
            if (!File.Exists(uinputPath))
            {
                _logger.LogError("uinput device not found at {Path}", uinputPath);
                return;
            }

            // Use evemu-event or direct uinput - for simplicity, use a subprocess
            // This is a pragmatic solution that works on Wayland
            var keyCode = (int)key;
            
            // Create uinput device, send key, destroy device
            // Using libevdev-based approach via shell
            var script = $@"
import os
import time
import struct
import fcntl

# uinput constants
UI_SET_EVBIT = 0x40045564
UI_SET_KEYBIT = 0x40045565
UI_DEV_CREATE = 0x5501
UI_DEV_DESTROY = 0x5502
EV_KEY = 0x01
EV_SYN = 0x00
SYN_REPORT = 0x00

# Open uinput
fd = os.open('/dev/uinput', os.O_WRONLY | os.O_NONBLOCK)

# Enable EV_KEY
fcntl.ioctl(fd, UI_SET_EVBIT, EV_KEY)

# Enable the specific key
fcntl.ioctl(fd, UI_SET_KEYBIT, {keyCode})

# uinput_user_dev structure (legacy)
# char name[80], id (bustype, vendor, product, version = 4x uint16), ff_effects_max, absmax[64], absmin[64], absfuzz[64], absflat[64]
name = b'virtual-assistant-kbd'
name = name + b'\x00' * (80 - len(name))
user_dev = name + struct.pack('<HHHHI', 0x03, 0x1234, 0x5678, 0x0001, 0)  # BUS_VIRTUAL
user_dev = user_dev + b'\x00' * (4 * 64 * 4)  # abs arrays

os.write(fd, user_dev)
fcntl.ioctl(fd, UI_DEV_CREATE)

time.sleep(0.1)  # Let device settle

# Send key press
def send_event(fd, ev_type, code, value):
    event = struct.pack('<QQHHi', 0, 0, ev_type, code, value)
    os.write(fd, event)

send_event(fd, EV_KEY, {keyCode}, 1)  # Press
send_event(fd, EV_SYN, SYN_REPORT, 0)

time.sleep(0.05)

send_event(fd, EV_KEY, {keyCode}, 0)  # Release
send_event(fd, EV_SYN, SYN_REPORT, 0)

time.sleep(0.1)

fcntl.ioctl(fd, UI_DEV_DESTROY)
os.close(fd)
";
            
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "python3",
                Arguments = $"-c \"{script.Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            using var process = System.Diagnostics.Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                var stderr = await process.StandardError.ReadToEndAsync();
                
                if (process.ExitCode != 0)
                {
                    _logger.LogError("Failed to simulate key press. Exit code: {ExitCode}, Error: {Error}", 
                        process.ExitCode, stderr);
                }
                else
                {
                    _logger.LogInformation("Successfully simulated key press: {Key}", key);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error simulating key press: {Key}", key);
        }
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
        _deviceStream?.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
