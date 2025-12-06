namespace VirtualAssistant.Core.Services;

/// <summary>
/// Interface for TTS notification service.
/// Used to break circular dependency between Core and Voice projects.
/// </summary>
public interface ITtsNotificationService
{
    /// <summary>
    /// Speaks the notification text using TTS.
    /// </summary>
    /// <param name="text">Text to speak</param>
    /// <param name="source">Source identifier for voice selection</param>
    /// <param name="ct">Cancellation token</param>
    Task SpeakAsync(string text, string? source = null, CancellationToken ct = default);
}
