using Tmds.DBus;

namespace Olbrasoft.VirtualAssistant.Desktop.DBus;

/// <summary>
/// D-Bus interface for org.freedesktop.Notifications.
/// Used to display desktop notifications.
/// </summary>
[DBusInterface("org.freedesktop.Notifications")]
public interface INotifications : IDBusObject
{
    /// <summary>
    /// Sends a notification to the desktop notification daemon.
    /// </summary>
    /// <param name="appName">Application name.</param>
    /// <param name="replacesId">ID of notification to replace (0 for new).</param>
    /// <param name="appIcon">Icon name or path.</param>
    /// <param name="summary">Notification title.</param>
    /// <param name="body">Notification body text.</param>
    /// <param name="actions">Array of action strings.</param>
    /// <param name="hints">Dictionary of hints.</param>
    /// <param name="expireTimeout">Timeout in milliseconds (-1 default, 0 never expires).</param>
    /// <returns>Notification ID that can be used to replace or close.</returns>
    Task<uint> NotifyAsync(
        string appName,
        uint replacesId,
        string appIcon,
        string summary,
        string body,
        string[] actions,
        IDictionary<string, object> hints,
        int expireTimeout);

    /// <summary>
    /// Closes a notification by ID.
    /// </summary>
    /// <param name="id">Notification ID to close.</param>
    Task CloseNotificationAsync(uint id);
}
