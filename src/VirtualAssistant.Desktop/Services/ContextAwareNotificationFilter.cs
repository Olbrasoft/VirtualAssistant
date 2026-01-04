using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using VirtualAssistant.Core.Models;
using VirtualAssistant.Core.Services;
using VirtualAssistant.Desktop.Configuration;

namespace VirtualAssistant.Desktop.Services;

/// <summary>
/// Context-aware notification filter that prevents redundant notifications
/// when user is already in the target application.
/// </summary>
public partial class ContextAwareNotificationFilter : INotificationFilter
{
    private readonly NotificationFilteringOptions _options;
    private readonly ILogger<ContextAwareNotificationFilter> _logger;

    // Regex pattern for extracting app names from notifications
    [GeneratedRegex(@"\b(Claude Code|GitHub|Chrome|OpenCode|VS Code|PyCharm|Telegram|WhatsApp|Rider|Firefox|Edge)\b", RegexOptions.IgnoreCase)]
    private static partial Regex AppMentionPattern();

    public ContextAwareNotificationFilter(
        IOptions<NotificationFilteringOptions> options,
        ILogger<ContextAwareNotificationFilter> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<bool> ShouldDeliverAsync(
        string notificationText,
        DesktopContext? context,
        CancellationToken ct = default)
    {
        // Extract notification context
        var notificationContext = ExtractContext(notificationText);

        // Always deliver if filtering disabled
        if (!_options.Enabled)
        {
            _logger.LogDebug("Filtering disabled, delivering notification");
            return Task.FromResult(true);
        }

        // Always deliver if urgent
        if (notificationContext.IsUrgent)
        {
            _logger.LogInformation(
                "Urgent notification, delivering regardless of context: {Text}",
                notificationText
            );
            return Task.FromResult(true);
        }

        // Always deliver if desktop context unavailable (safe fallback)
        if (context == null)
        {
            _logger.LogWarning(
                "Desktop context unavailable, delivering notification as fallback"
            );
            return Task.FromResult(true);
        }

        // Always deliver for certain sources
        if (_options.AlwaysDeliverSources.Contains(notificationContext.Source))
        {
            _logger.LogDebug(
                "Source {Source} in always-deliver list, delivering notification",
                notificationContext.Source
            );
            return Task.FromResult(true);
        }

        // Skip if notification is about app user is currently using
        if (notificationContext.TargetApplication != null)
        {
            var currentAppLower = context.ActiveApplication.ToLowerInvariant();
            var targetAppId = MapAppNameToAppId(notificationContext.TargetApplication);

            if (currentAppLower.Contains(targetAppId.ToLowerInvariant()))
            {
                _logger.LogInformation(
                    "User already in {App}, skipping notification: {Text}",
                    notificationContext.TargetApplication,
                    notificationText
                );
                return Task.FromResult(false); // SKIP notification
            }
        }

        // Default: deliver notification
        _logger.LogDebug("Delivering notification: {Text}", notificationText);
        return Task.FromResult(true);
    }

    public NotificationContext ExtractContext(string notificationText)
    {
        // Extract app name from text
        var match = AppMentionPattern().Match(notificationText);
        var targetApp = match.Success ? match.Value : null;

        // Detect if urgent (contains keywords)
        var isUrgent = notificationText.Contains("urgent", StringComparison.OrdinalIgnoreCase)
            || notificationText.Contains("critical", StringComparison.OrdinalIgnoreCase)
            || notificationText.Contains("error", StringComparison.OrdinalIgnoreCase);

        // Detect source
        var source = DetectSource(notificationText);

        return new NotificationContext(targetApp, isUrgent, source);
    }

    private NotificationSource DetectSource(string text)
    {
        if (text.Contains("dokončil", StringComparison.OrdinalIgnoreCase)
            || text.Contains("completed", StringComparison.OrdinalIgnoreCase))
            return NotificationSource.TaskCompletion;

        if (text.Contains("GitHub", StringComparison.OrdinalIgnoreCase)
            || text.Contains("issue", StringComparison.OrdinalIgnoreCase))
            return NotificationSource.GitHubEvent;

        if (text.Contains("system", StringComparison.OrdinalIgnoreCase)
            || text.Contains("alert", StringComparison.OrdinalIgnoreCase))
            return NotificationSource.SystemAlert;

        return NotificationSource.UserMessage;
    }

    private string MapAppNameToAppId(string appName)
    {
        // Case-insensitive lookup in mapping dictionary
        var kvp = _options.AppNameMapping.FirstOrDefault(
            x => x.Key.Equals(appName, StringComparison.OrdinalIgnoreCase));

        // Check if we found a match (KeyValuePair is a struct, so check against default)
        if (!Equals(kvp, default(KeyValuePair<string, string>)))
            return kvp.Value;

        // Fallback: use app name as-is
        return appName;
    }
}
