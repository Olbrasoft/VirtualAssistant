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
    public IReadOnlyList<string> FindKeyboardDevices()
    {
        // Stable by-id paths survive event-number changes across reconnects.
        if (Directory.Exists(ByIdDirectory))
        {
            var kbdDevices = Directory.GetFiles(ByIdDirectory)
                .Where(f => f.Contains("kbd", StringComparison.OrdinalIgnoreCase)
                         && f.Contains("event", StringComparison.OrdinalIgnoreCase))
                .Where(f => !f.Contains("Mouse", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.Ordinal)
                .GroupBy(GetCanonicalPath, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();

            if (kbdDevices.Count > 0)
            {
                _logger.LogInformation("Found {Count} keyboard devices by-id: {Devices}",
                    kbdDevices.Count, string.Join(", ", kbdDevices));
                return kbdDevices;
            }
        }

        // Fallback: scan /dev/input/eventX and check capabilities
        var eventDevices = Directory.GetFiles(InputDirectory, "event*")
            .OrderBy(d => d)
            .ToList();

        var detectedDevices = eventDevices.Where(IsKeyboardDevice).ToList();
        if (detectedDevices.Count > 0)
        {
            _logger.LogInformation("Found {Count} keyboard devices: {Devices}",
                detectedDevices.Count, string.Join(", ", detectedDevices));
            return detectedDevices;
        }

        // Last resort: use event0
        _logger.LogWarning("Could not detect keyboard device, using fallback: {Device}", FallbackDevice);
        return [FallbackDevice];
    }

    /// <inheritdoc />
    public string FindKeyboardDevice() => FindKeyboardDevices()[0];

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

    private static string GetCanonicalPath(string devicePath)
    {
        try
        {
            return File.ResolveLinkTarget(devicePath, returnFinalTarget: true)?.FullName
                ?? Path.GetFullPath(devicePath);
        }
        catch (IOException)
        {
            return Path.GetFullPath(devicePath);
        }
    }
}
