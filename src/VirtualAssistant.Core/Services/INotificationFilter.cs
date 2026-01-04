using Olbrasoft.VirtualAssistant.Core.Models;

namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Service for filtering notifications based on desktop context.
/// Prevents redundant notifications when user is already in the target application.
/// </summary>
public interface INotificationFilter
{
    /// <summary>
    /// Determines if notification should be delivered based on desktop context.
    /// </summary>
    /// <param name="notificationText">The notification text to analyze.</param>
    /// <param name="context">Current desktop context (can be null if unavailable).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// True if notification should be delivered, false to skip.
    /// Always returns true for urgent notifications or when context is unavailable.
    /// </returns>
    Task<bool> ShouldDeliverAsync(
        string notificationText,
        DesktopContext? context,
        CancellationToken ct = default);

    /// <summary>
    /// Extracts notification context from notification text.
    /// Analyzes text to determine target application, urgency, and source.
    /// </summary>
    /// <param name="notificationText">The notification text to analyze.</param>
    /// <returns>NotificationContext with extracted information.</returns>
    NotificationContext ExtractContext(string notificationText);
}
