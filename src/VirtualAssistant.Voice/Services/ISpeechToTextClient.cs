namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Client for querying SpeechToText application status.
/// </summary>
public interface ISpeechToTextClient
{
    /// <summary>
    /// Gets the current status of the SpeechToText application.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Status response, or null if the service is unavailable.</returns>
    Task<SpeechToTextStatus?> GetStatusAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Status response from SpeechToText application.
/// </summary>
public record SpeechToTextStatus
{
    /// <summary>
    /// Gets a value indicating whether audio recording is currently in progress.
    /// </summary>
    public bool IsRecording { get; init; }

    /// <summary>
    /// Gets a value indicating whether Whisper transcription is currently in progress.
    /// </summary>
    public bool IsTranscribing { get; init; }

    /// <summary>
    /// Gets the current state as a string (e.g., "Recording", "Idle", "Transcribing").
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Gets a value indicating whether user is busy (recording or transcribing).
    /// </summary>
    public bool IsBusy => IsRecording || IsTranscribing;
}
