namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Tracks what the assistant is currently saying (TTS output).
/// Used to filter out assistant's own speech from transcriptions.
/// </summary>
public interface IAssistantSpeechTrackerService
{
    /// <summary>
    /// Returns true if assistant is currently speaking (TTS active).
    /// Used for AEC logging.
    /// </summary>
    bool IsSpeaking { get; }

    /// <summary>
    /// Called when assistant starts speaking. Adds to history instead of replacing.
    /// </summary>
    void StartSpeaking(string text);

    /// <summary>
    /// Called when assistant stops speaking.
    /// </summary>
    void StopSpeaking();

    /// <summary>
    /// Filters out all TTS echo messages from the transcription.
    /// Iterates through TTS history and removes matching prefixes.
    /// </summary>
    /// <param name="transcription">The full transcription from Whisper</param>
    /// <returns>Cleaned text with TTS echoes removed</returns>
    string FilterEchoFromTranscription(string transcription);

    /// <summary>
    /// Gets the TTS history count (for debugging).
    /// </summary>
    int GetHistoryCount();

    /// <summary>
    /// Clears the TTS history.
    /// </summary>
    void ClearHistory();

    /// <summary>
    /// Checks if any TTS message in history contains one of the stop words.
    /// Used to distinguish between user's "stop" command and TTS echo.
    /// </summary>
    /// <param name="stopWords">Collection of stop words to check for</param>
    /// <returns>True if any TTS message contains a stop word</returns>
    bool ContainsStopWord(IEnumerable<string> stopWords);
}
