namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Manages audio buffers for speech recording.
/// </summary>
public interface ISpeechBufferManager
{
    /// <summary>
    /// Adds a chunk to the pre-buffer (used before speech is detected).
    /// </summary>
    void AddToPreBuffer(byte[] chunk);

    /// <summary>
    /// Transfers all pre-buffer contents to the speech buffer.
    /// </summary>
    void TransferPreBufferToSpeech();

    /// <summary>
    /// Adds a chunk directly to the speech buffer.
    /// </summary>
    void AddToSpeechBuffer(byte[] chunk);

    /// <summary>
    /// Gets all speech data combined into a single array.
    /// </summary>
    byte[] GetCombinedSpeechData();

    /// <summary>
    /// Gets the total size of speech buffer in bytes.
    /// </summary>
    int SpeechBufferSize { get; }

    /// <summary>
    /// Clears the speech buffer.
    /// </summary>
    void ClearSpeechBuffer();

    /// <summary>
    /// Clears all buffers (pre-buffer and speech buffer).
    /// </summary>
    void ClearAll();
}
