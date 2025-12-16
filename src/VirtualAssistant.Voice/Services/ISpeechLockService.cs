namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Service for managing speech lock state (microphone active detection).
/// When locked, TTS playback should be paused to avoid interference with recording.
/// </summary>
public interface ISpeechLockService
{
    /// <summary>
    /// Gets whether speech is currently locked (microphone is active or programmatically locked).
    /// </summary>
    bool IsLocked { get; }

    /// <summary>
    /// Activates speech lock (recording started).
    /// Stops current TTS and queues new messages.
    /// Auto-unlocks after timeout for safety.
    /// </summary>
    /// <param name="timeout">Optional timeout after which lock auto-releases. Default is 30 seconds.</param>
    void Lock(TimeSpan? timeout = null);

    /// <summary>
    /// Deactivates speech lock (recording stopped).
    /// Should trigger queue flush to play pending messages.
    /// </summary>
    void Unlock();
}
