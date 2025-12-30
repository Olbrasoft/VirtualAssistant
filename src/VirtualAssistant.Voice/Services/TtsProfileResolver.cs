using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Resolves application-specific TTS profiles for text-to-speech configuration.
/// Maps application names to their configured voice, rate, pitch, and priority settings.
/// </summary>
public class TtsProfileResolver : ITtsProfileResolver
{
    private readonly IOptions<TtsProfilesOptions> _options;
    private readonly ILogger<TtsProfileResolver> _logger;

    public TtsProfileResolver(
        IOptions<TtsProfilesOptions> options,
        ILogger<TtsProfileResolver> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Gets TTS voice configuration for a specific application.
    /// </summary>
    /// <param name="appName">Application name (e.g., "claude-code", "desktop-monitor"). If null, returns default profile.</param>
    /// <returns>Voice configuration for the application or default if not found.</returns>
    public VoiceConfig GetProfileForApplication(string? appName)
    {
        var profiles = _options.Value;

        // Try to find profile by app name
        if (!string.IsNullOrEmpty(appName) &&
            profiles.Profiles.TryGetValue(appName, out var profile))
        {
            _logger.LogDebug("Using TTS profile for application: {AppName} (Voice: {Voice}, Rate: {Rate})",
                appName, profile.Voice, profile.Rate);
            return MapToVoiceConfig(profile);
        }

        // Fallback to default profile
        _logger.LogDebug("Using default TTS profile for application: {AppName} (Voice: {Voice})",
            appName ?? "unknown", profiles.DefaultProfile.Voice);
        return MapToVoiceConfig(profiles.DefaultProfile);
    }

    /// <summary>
    /// Maps TtsProfile configuration to VoiceConfig record used by TTS services.
    /// </summary>
    private static VoiceConfig MapToVoiceConfig(TtsProfile profile)
    {
        return new VoiceConfig(
            Voice: profile.Voice,
            Rate: profile.Rate,
            Volume: "+0%", // Volume is intentionally fixed at default and not configurable per TtsProfile
            Pitch: profile.Pitch
        );
    }
}
