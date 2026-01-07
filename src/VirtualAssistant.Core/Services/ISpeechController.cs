using Olbrasoft.VirtualAssistant.Core.Models;

namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Controls TTS speech operations.
/// Use this interface when you need to speak text.
/// </summary>
public interface ISpeechController
{
    /// <summary>
    /// Speaks the text using TTS with agent-specific voice selection.
    /// </summary>
    /// <param name="text">Text to speak</param>
    /// <param name="agentName">Optional agent name for voice selection (e.g., "gemini", "claude-code", "opencode"). Defaults to "assistant".</param>
    /// <param name="skipCache">If true, bypasses TTS cache and always generates fresh audio (use for notifications with dynamic content)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the TTS operation including provider used and duration.</returns>
    Task<TtsResult> SpeakAsync(string text, string? agentName = null, bool skipCache = false, CancellationToken ct = default);
}
