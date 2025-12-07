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

    /// <summary>
    /// Speaks the notification text using TTS, but skips if user is on same workspace as agent.
    /// </summary>
    /// <param name="text">Text to speak</param>
    /// <param name="agentName">Agent name for workspace detection (e.g., "opencode", "claude")</param>
    /// <param name="source">Source identifier for voice selection</param>
    /// <param name="ct">Cancellation token</param>
    Task SpeakIfNotOnAgentWorkspaceAsync(string text, string agentName, string? source = null, CancellationToken ct = default);
}
