using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Discovers keyboard devices on Linux using /dev/input/ filesystem.
/// Checks device capabilities to identify keyboards vs other input devices.
/// </summary>
public class LinuxKeyboardDeviceDiscovery : IKeyboardDeviceDiscovery
{
    private readonly ILogger<LinuxKeyboardDeviceDiscovery> _logger;
    private const string InputDirectory = "/dev/input";
    private const string ByIdDirectory = "/dev/input/by-id";
    private const string FallbackDevice = "/dev/input/event0";

    public LinuxKeyboardDeviceDiscovery(ILogger<LinuxKeyboardDeviceDiscovery> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string FindKeyboardDevice()
    {
        // Try to find by-id first (more reliable)
        if (Directory.Exists(ByIdDirectory))
        {
            var kbdDevices = Directory.GetFiles(ByIdDirectory)
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
        var eventDevices = Directory.GetFiles(InputDirectory, "event*")
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
        _logger.LogWarning("Could not detect keyboard device, using fallback: {Device}", FallbackDevice);
        return FallbackDevice;
    }

    /// <inheritdoc />
    public bool IsKeyboardDevice(string devicePath)
    {
        try
        {
            var deviceName = Path.GetFileName(devicePath);
            var capsPath = $"/sys/class/input/{deviceName}/device/capabilities/key";

            if (!File.Exists(capsPath))
            {
                _logger.LogDebug("Capabilities file not found: {Path}", capsPath);
                return false;
            }

            var caps = File.ReadAllText(capsPath).Trim();

            // Keyboard devices typically have extensive key capabilities
            // A simple heuristic: keyboards have more than 20 characters in capabilities
            // and not all zeros
            var isKeyboard = caps.Length > 20 && !caps.All(c => c == '0' || c == ' ');

            _logger.LogDebug("Device {Device} keyboard check: {IsKeyboard} (caps length: {Length})",
                devicePath, isKeyboard, caps.Length);

            return isKeyboard;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking if {Device} is keyboard", devicePath);
            return false;
        }
    }
}
