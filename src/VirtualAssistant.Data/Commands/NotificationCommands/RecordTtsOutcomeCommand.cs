using Olbrasoft.Data.Cqrs;

namespace Olbrasoft.VirtualAssistant.Data.Commands.NotificationCommands;

/// <summary>
/// Command to record the final TTS outcome for a notification.
/// </summary>
/// <param name="NotificationId">ID of the notification.</param>
/// <param name="ProviderName">Name of the provider that succeeded (or null if all failed).</param>
/// <param name="Status">Final TTS status ("success", "error", "timeout", "all_failed", "skipped").</param>
/// <param name="DurationMs">Duration of TTS operation in milliseconds.</param>
public record RecordTtsOutcomeCommand(
    int NotificationId,
    string? ProviderName,
    string Status,
    int? DurationMs = null
) : ICommand<bool>;
