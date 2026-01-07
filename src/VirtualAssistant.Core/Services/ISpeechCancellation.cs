namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Controls speech cancellation and queue management.
/// Use this interface when you need to cancel or manage speech queue.
/// </summary>
public interface ISpeechCancellation
{
    /// <summary>
    /// Cancels currently playing speech.
    /// Next item in queue will start playing.
    /// </summary>
    void CancelCurrentSpeech();

    /// <summary>
    /// Cancels all speech and clears the queue.
    /// </summary>
    void CancelAllSpeech();

    /// <summary>
    /// Plays all queued messages immediately.
    /// Called when speech lock is released to flush pending messages.
    /// </summary>
    Task FlushQueueAsync(CancellationToken ct = default);
}
