using Olbrasoft.SystemTray.Linux;
using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Infrastructure;

/// <summary>
/// Adapter for SystemTray.Linux TrayIconManager to ITrayIconManager interface.
/// Implements Adapter pattern for dependency injection and testing.
/// </summary>
public class SystemTrayIconManagerAdapter : Core.Services.ITrayIconManager
{
    private readonly TrayIconManager _manager;

    public SystemTrayIconManagerAdapter(TrayIconManager manager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    public async Task<Core.Services.ITrayIcon?> CreateIconAsync(
        string id,
        string iconPath,
        string tooltip,
        Core.Services.ITrayMenuHandler? menuHandler)
    {
        // Convert ITrayMenuHandler to Olbrasoft.SystemTray.Linux.ITrayMenuHandler
        SystemTray.Linux.ITrayMenuHandler? linuxMenuHandler = null;
        if (menuHandler is SystemTray.Linux.ITrayMenuHandler handler)
        {
            linuxMenuHandler = handler;
        }

        var icon = await _manager.CreateIconAsync(id, iconPath, tooltip, linuxMenuHandler);
        return icon != null ? new TrayIconAdapter(icon) : null;
    }

    public void RemoveIcon(string id)
    {
        _manager.RemoveIcon(id);
    }

    /// <summary>
    /// Adapter for SystemTray.Linux ITrayIcon to Core ITrayIcon interface.
    /// </summary>
    private class TrayIconAdapter : Core.Services.ITrayIcon
    {
        private readonly SystemTray.Linux.ITrayIcon _icon;

        public TrayIconAdapter(SystemTray.Linux.ITrayIcon icon)
        {
            _icon = icon ?? throw new ArgumentNullException(nameof(icon));
        }

        public void SetIcon(string iconPath, string tooltip)
        {
            _icon.SetIcon(iconPath, tooltip);
        }
    }
}
