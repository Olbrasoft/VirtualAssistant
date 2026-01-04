using Olbrasoft.VirtualAssistant.Core.Models;

namespace Olbrasoft.VirtualAssistant.Desktop.Configuration;

/// <summary>
/// Configuration options for context-aware notification filtering.
/// </summary>
public class NotificationFilteringOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "NotificationFiltering";

    /// <summary>
    /// Whether notification filtering is enabled.
    /// If false, all notifications are delivered regardless of context.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maps friendly application names (mentioned in notifications) to actual app IDs.
    /// Example: "Claude Code" -> "code", "GitHub" -> "chrome"
    /// </summary>
    public Dictionary<string, string> AppNameMapping { get; set; } = new();

    /// <summary>
    /// Notification sources that should always be delivered, regardless of context.
    /// Example: SystemAlert, UserMessage
    /// </summary>
    public NotificationSource[] AlwaysDeliverSources { get; set; } = Array.Empty<NotificationSource>();
}
