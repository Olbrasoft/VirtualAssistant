namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Resolves application-specific TTS profiles for text-to-speech configuration.
/// </summary>
public interface ITtsProfileResolver
{
    /// <summary>
    /// Gets TTS voice configuration for a specific application.
    /// </summary>
    /// <param name="appName">Application name (e.g., "claude-code", "desktop-monitor"). If null, returns default profile.</param>
    /// <returns>Voice configuration for the application or default if not found.</returns>
    VoiceConfig GetProfileForApplication(string? appName);
}
