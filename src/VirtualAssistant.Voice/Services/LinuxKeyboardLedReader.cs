using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Reads keyboard LED states from Linux /sys/class/leds/ filesystem.
/// Note: May not work reliably on Wayland.
/// </summary>
public class LinuxKeyboardLedReader : IKeyboardLedReader
{
    private readonly ILogger<LinuxKeyboardLedReader> _logger;
    private const string LedsDirectory = "/sys/class/leds";

    public LinuxKeyboardLedReader(ILogger<LinuxKeyboardLedReader> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public bool IsCapsLockOn()
    {
        return ReadLedState("capslock");
    }

    /// <inheritdoc />
    public bool IsScrollLockOn()
    {
        return ReadLedState("scrolllock");
    }

    /// <inheritdoc />
    public bool IsNumLockOn()
    {
        return ReadLedState("numlock");
    }

    /// <summary>
    /// Reads LED state from /sys/class/leds/.
    /// </summary>
    /// <param name="ledName">LED name (e.g., "capslock", "scrolllock", "numlock").</param>
    /// <returns>True if LED is on (brightness > 0), false otherwise.</returns>
    private bool ReadLedState(string ledName)
    {
        try
        {
            if (!Directory.Exists(LedsDirectory))
            {
                _logger.LogWarning("LEDs directory {Directory} does not exist", LedsDirectory);
                return false;
            }

            var led = Directory.GetDirectories(LedsDirectory)
                .FirstOrDefault(d => d.Contains(ledName, StringComparison.OrdinalIgnoreCase));

            if (led == null)
            {
                _logger.LogDebug("LED {LedName} not found in {Directory}", ledName, LedsDirectory);
                return false;
            }

            var brightnessPath = Path.Combine(led, "brightness");
            if (!File.Exists(brightnessPath))
            {
                _logger.LogWarning("Brightness file not found: {Path}", brightnessPath);
                return false;
            }

            var value = File.ReadAllText(brightnessPath).Trim();
            return value != "0";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read LED state for {Led}", ledName);
            return false;
        }
    }
}
