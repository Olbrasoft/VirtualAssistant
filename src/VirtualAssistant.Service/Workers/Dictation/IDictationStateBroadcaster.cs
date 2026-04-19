using Olbrasoft.VirtualAssistant.Core.StateMachine;

namespace Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

/// <summary>
/// SignalR broadcast bridge lifted out of <c>DictationWorker</c> (#969
/// god-class split). Owns the subscriptions to the state machine's
/// <c>StateChanged</c> event, the transcriber's <c>RawTranscriptionReady</c>
/// event, and the worker's own <c>TranscriptionCompleted</c> event, and
/// fans every emission into <see cref="IDictationOutputChannel.BroadcastEventAsync"/>
/// on the dictation SignalR hub.
/// </summary>
public interface IDictationStateBroadcaster
{
    /// <summary>
    /// Wire event subscriptions. <paramref name="subscribeTranscriptionCompleted"/>
    /// and <paramref name="unsubscribeTranscriptionCompleted"/> are the
    /// worker-scoped event subscription hooks (worker owns the event so the
    /// broadcaster subscribes via add/remove callbacks rather than a direct
    /// reference to the event itself).
    /// </summary>
    void Start(
        Action<EventHandler<string>> subscribeTranscriptionCompleted,
        Action<EventHandler<string>> unsubscribeTranscriptionCompleted);

    /// <summary>
    /// Unsubscribe from all events. Safe to call when <see cref="Start"/>
    /// was never invoked.
    /// </summary>
    void Stop();
}
