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

    public EvdevKeyboardMonitor(ILogger<EvdevKeyboardMonitor> logger, string? devicePath = null)
    {
        _logger = logger;
        _devicePath = devicePath ?? FindKeyboardDevice();
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
        return ReadLedState("scrolllock");
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
                if (keyCode != KeyCode.ScrollLock && keyCode != KeyCode.CapsLock && keyCode != KeyCode.Escape)
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
    /// Reads LED state from /sys/class/leds/.
    /// Note: This may not work on Wayland.
    /// </summary>
    private bool ReadLedState(string ledName)
    {
        try
        {
            var ledsDir = "/sys/class/leds";
            if (!Directory.Exists(ledsDir))
                return false;

            var led = Directory.GetDirectories(ledsDir)
                .FirstOrDefault(d => d.Contains(ledName, StringComparison.OrdinalIgnoreCase));

            if (led == null)
                return false;

            var brightnessPath = Path.Combine(led, "brightness");
            if (!File.Exists(brightnessPath))
                return false;

            var value = File.ReadAllText(brightnessPath).Trim();
            return value != "0";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read LED state for {Led}", ledName);
            return false;
        }
    }

    /// <summary>
    /// Finds the first keyboard device in /dev/input/.
    /// Prefers actual keyboards over mouse-embedded keyboards.
    /// </summary>
    private string FindKeyboardDevice()
    {
        var inputDir = "/dev/input";
        
        // Try to find by-id first (more reliable)
        var byIdDir = "/dev/input/by-id";
        if (Directory.Exists(byIdDir))
        {
            var kbdDevices = Directory.GetFiles(byIdDir)
                .Where(f => f.Contains("kbd", StringComparison.OrdinalIgnoreCase) 
                         && f.Contains("event", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Prefer devices that are NOT mice (mice can have embedded keyboards)
            var kbdDevice = kbdDevices
                .FirstOrDefault(f => !f.Contains("Mouse", StringComparison.OrdinalIgnoreCase))
                ?? kbdDevices.FirstOrDefault();

            if (kbdDevice != null)
            {
                _logger.LogInformation("Found keyboard device by-id: {Device}", kbdDevice);
                return kbdDevice;
            }
        }

        // Fallback: scan /dev/input/eventX and check capabilities
        var eventDevices = Directory.GetFiles(inputDir, "event*")
            .OrderBy(d => d)
            .ToList();

        foreach (var device in eventDevices)
        {
            if (IsKeyboardDevice(device))
            {
                _logger.LogInformation("Found keyboard device: {Device}", device);
                return device;
            }
        }

        // Last resort: use event0
        var fallback = "/dev/input/event0";
        _logger.LogWarning("Could not detect keyboard device, using fallback: {Device}", fallback);
        return fallback;
    }

    /// <summary>
    /// Checks if device is a keyboard by reading its capabilities.
    /// </summary>
    private bool IsKeyboardDevice(string devicePath)
    {
        try
        {
            var deviceName = Path.GetFileName(devicePath);
            var capsPath = $"/sys/class/input/{deviceName}/device/capabilities/key";

            if (!File.Exists(capsPath))
                return false;

            var caps = File.ReadAllText(capsPath).Trim();
            
            // Keyboard devices typically have extensive key capabilities
            // A simple heuristic: keyboards have more than 20 characters in capabilities
            return caps.Length > 20 && !caps.All(c => c == '0' || c == ' ');
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        GC.SuppressFinalize(this);
    }
}
