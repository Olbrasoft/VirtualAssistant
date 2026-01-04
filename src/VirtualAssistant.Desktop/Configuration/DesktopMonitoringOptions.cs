namespace VirtualAssistant.Desktop.Configuration;

/// <summary>
/// Configuration options for desktop monitoring services.
/// </summary>
public class DesktopMonitoringOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "DesktopMonitoring";

    /// <summary>
    /// Enable desktop context monitoring (default: true).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Polling interval in milliseconds for window focus changes (default: 500ms).
    /// </summary>
    public int PollingIntervalMs { get; set; } = 500;

    /// <summary>
    /// Gracefully degrade when GNOME extensions are missing (default: true).
    /// When true, services will log warnings but not crash when D-Bus services are unavailable.
    /// </summary>
    public bool GracefulDegradation { get; set; } = true;

    /// <summary>
    /// Log desktop context changes (default: true).
    /// </summary>
    public bool LogContextChanges { get; set; } = true;
}
