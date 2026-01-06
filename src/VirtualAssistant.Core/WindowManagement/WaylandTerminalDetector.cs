using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Core.WindowManagement;

/// <summary>
/// Terminal detector implementation using LinuxDesktop DesktopContextService.
/// Detects if the active window is a terminal application by checking ActiveApplication.
/// </summary>
public class WaylandTerminalDetector : ITerminalDetector
{
    private readonly ILogger<WaylandTerminalDetector> _logger;
    private readonly IDesktopContextService _desktopContextService;

    /// <summary>
    /// Terminal application identifiers (.desktop file names) that require Ctrl+Shift+V for pasting.
    /// </summary>
    private static readonly HashSet<string> TerminalAppIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "kitty.desktop",
        "org.gnome.Terminal.desktop",
        "gnome-terminal.desktop",
        "konsole.desktop",
        "xfce4-terminal.desktop",
        "mate-terminal.desktop",
        "tilix.desktop",
        "terminator.desktop",
        "Alacritty.desktop",
        "org.wezfurlong.wezterm.desktop",
        "foot.desktop",
        "xterm.desktop",
        "urxvt.desktop",
        "st.desktop",
        "terminology.desktop"
    };

    public WaylandTerminalDetector(
        ILogger<WaylandTerminalDetector> logger,
        IDesktopContextService desktopContextService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _desktopContextService = desktopContextService ?? throw new ArgumentNullException(nameof(desktopContextService));
    }

    public async Task<bool> IsTerminalActiveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Get current desktop context from LinuxDesktop monitoring
            var context = await _desktopContextService.GetCurrentContextAsync(cancellationToken);

            // If desktop monitoring is unavailable (D-Bus frozen, extension not running),
            // default to terminal paste (Ctrl+Shift+V) as it's safer and works in more cases
            if (string.IsNullOrEmpty(context.ActiveApplication) || context.ActiveApplication == "Unknown")
            {
                _logger.LogWarning("Desktop monitoring unavailable (ActiveApplication: {ActiveApp}), defaulting to terminal paste (Ctrl+Shift+V) for safety",
                    context.ActiveApplication ?? "(null)");
                return true; // Default to terminal paste when unknown
            }

            // Check if active application is a known terminal
            if (TerminalAppIds.Contains(context.ActiveApplication))
            {
                _logger.LogDebug("Detected terminal application: {ActiveApp} (window: {WindowTitle})",
                    context.ActiveApplication, context.ActiveWindowTitle);
                return true;
            }

            _logger.LogDebug("Non-terminal application detected: {ActiveApp} (window: {WindowTitle})",
                context.ActiveApplication, context.ActiveWindowTitle);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error detecting terminal application, defaulting to terminal paste (Ctrl+Shift+V) for safety");
            return true; // Default to terminal paste on error
        }
    }

}
