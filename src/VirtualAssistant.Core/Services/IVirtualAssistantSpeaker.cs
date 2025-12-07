namespace VirtualAssistant.Core.Services;

/// <summary>
/// Single entry point for all TTS operations in VirtualAssistant.
/// All components must use this interface for speaking - nobody injects ITtsService directly.
/// </summary>
public interface IVirtualAssistantSpeaker
{
    /// <summary>
    /// Speaks the text using TTS.
    /// If agentName is provided, checks workspace and skips TTS if user is on same workspace as agent.
    /// </summary>
    /// <param name="text">Text to speak</param>
    /// <param name="agentName">Optional agent name for workspace detection (e.g., "opencode", "claude")</param>
    /// <param name="ct">Cancellation token</param>
    Task SpeakAsync(string text, string? agentName = null, CancellationToken ct = default);
}
