namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Sends TTS notifications for Claude execution events.
/// </summary>
public interface IClaudeNotificationSender
{
    /// <summary>
    /// Send error notification via TTS.
    /// </summary>
    /// <param name="message">Error message to speak</param>
    Task NotifyErrorAsync(string message);

    /// <summary>
    /// Send success notification via TTS.
    /// Only sends if NotifyOnSuccess is enabled in configuration.
    /// </summary>
    /// <param name="message">Success message to speak</param>
    Task NotifySuccessAsync(string message);
}
