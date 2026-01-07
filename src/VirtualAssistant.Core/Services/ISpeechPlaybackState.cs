namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Provides information about current speech playback state.
/// Use this interface when you only need to check if speech is active.
/// </summary>
public interface ISpeechPlaybackState
{
    /// <summary>
    /// Whether speech is currently playing (includes both generation and playback).
    /// </summary>
    bool IsSpeaking { get; }

    /// <summary>
    /// Whether audio is currently playing (excludes generation phase).
    /// Use this to check if user can actually hear the speech.
    /// </summary>
    bool IsPlaying { get; }
}
