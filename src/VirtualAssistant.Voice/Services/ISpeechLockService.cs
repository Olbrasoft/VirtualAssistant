namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Service for managing speech lock state (microphone active detection).
/// When locked, TTS playback should be paused to avoid interference with recording.
/// </summary>
public interface ISpeechLockService
{
    /// <summary>
    /// Gets whether speech is currently locked (microphone is active).
    /// </summary>
    bool IsLocked { get; }
}

/// <summary>
/// File-based speech lock service implementation.
/// Checks for existence of a lock file to determine if microphone is active.
/// </summary>
public sealed class SpeechLockService : ISpeechLockService
{
    private readonly string _lockFilePath;

    public SpeechLockService(string lockFilePath = "/tmp/speech-lock")
    {
        _lockFilePath = lockFilePath;
    }

    /// <inheritdoc />
    public bool IsLocked => File.Exists(_lockFilePath);
}
