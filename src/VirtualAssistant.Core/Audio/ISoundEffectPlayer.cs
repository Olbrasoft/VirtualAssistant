namespace Olbrasoft.VirtualAssistant.Core.Audio;

/// <summary>
/// Interface for sound effect players.
/// Enables dependency inversion for audio playback components.
/// </summary>
public interface ISoundEffectPlayer
{
    /// <summary>
    /// Plays the sound effect once.
    /// </summary>
    void Play();

    /// <summary>
    /// Starts continuous looping playback of the sound effect.
    /// </summary>
    void StartLoop();

    /// <summary>
    /// Stops the continuous looping playback.
    /// </summary>
    void StopLoop();
}
