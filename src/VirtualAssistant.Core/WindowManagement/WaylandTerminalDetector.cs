using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.WindowManagement;

/// <summary>
/// Terminal detector implementation for GNOME Wayland using window-calls D-Bus extension.
/// Detects if the active window is a terminal application by checking WM_CLASS property.
/// </summary>
public class WaylandTerminalDetector : ITerminalDetector
{
    private readonly ILogger<WaylandTerminalDetector> _logger;

    /// <summary>
    /// Terminal window class names that require Ctrl+Shift+V for pasting.
    /// </summary>
    private static readonly HashSet<string> TerminalClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "kitty",
        "gnome-terminal",
        "gnome-terminal-server",
        "org.gnome.Terminal",
        "konsole",
        "xfce4-terminal",
        "mate-terminal",
        "tilix",
        "terminator",
        "alacritty",
        "wezterm",
        "foot",
        "xterm",
        "urxvt",
        "st",
        "terminology"
    };

    public WaylandTerminalDetector(ILogger<WaylandTerminalDetector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> IsTerminalActiveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var windowClass = await GetActiveWindowClassAsync(cancellationToken);

            if (!string.IsNullOrEmpty(windowClass) && TerminalClasses.Contains(windowClass))
            {
                _logger.LogDebug("Detected terminal window: {WindowClass}", windowClass);
                return true;
            }

            _logger.LogDebug("Non-terminal window detected: {WindowClass}", windowClass ?? "(unknown)");
            return false;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Process error detecting window class, assuming non-terminal");
            return false;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start process for window class detection, assuming non-terminal");
            return false;
        }
    }

    /// <summary>
    /// Gets the WM_CLASS of the currently active window using window-calls GNOME Shell extension.
    /// Uses the List method and finds the window with focus=true.
    /// </summary>
    private async Task<string?> GetActiveWindowClassAsync(CancellationToken cancellationToken)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "gdbus",
                    Arguments = "call --session --dest org.gnome.Shell " +
                               "--object-path /org/gnome/Shell/Extensions/Windows " +
                               "--method org.gnome.Shell.Extensions.Windows.List",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                _logger.LogDebug("D-Bus window-calls returned: {Output}", output.Trim());

                // Output format is: ('[{"wm_class":"kitty",...,"focus":true},...]',)
                // Extract the JSON array from the gdbus output
                var jsonStart = output.IndexOf('[');
                var jsonEnd = output.LastIndexOf(']');

                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonArray = output.Substring(jsonStart, jsonEnd - jsonStart + 1);

                    // Parse JSON and find focused window
                    var windows = JsonSerializer.Deserialize<JsonElement>(jsonArray);

                    foreach (var window in windows.EnumerateArray())
                    {
                        if (window.TryGetProperty("focus", out var focusProp) && focusProp.GetBoolean())
                        {
                            if (window.TryGetProperty("wm_class", out var wmClassProp))
                            {
                                var windowClass = wmClassProp.GetString();
                                _logger.LogDebug("Focused window class: {WindowClass}", windowClass);
                                return windowClass;
                            }
                        }
                    }

                    _logger.LogWarning("No focused window found in window list");
                }
                else
                {
                    _logger.LogWarning("Could not find JSON array in output: {Output}", output.Trim());
                }
            }
            else
            {
                var error = await process.StandardError.ReadToEndAsync(cancellationToken);
                _logger.LogWarning("D-Bus call failed: ExitCode={ExitCode}, Error={Error}",
                    process.ExitCode, error?.Trim());
            }

            return null;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogDebug(ex, "Process error getting active window class via D-Bus");
            return null;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogDebug(ex, "Failed to start gdbus process for active window class");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Failed to parse D-Bus JSON response for window class");
            return null;
        }
    }
}
