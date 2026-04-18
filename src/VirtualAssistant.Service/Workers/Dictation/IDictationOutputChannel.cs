using Olbrasoft.VirtualAssistant.Service.Hubs;

namespace Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

/// <summary>
/// Output surface the dictation worker talks to when producing user-visible
/// feedback: the typing cue loop, the cancel earcon, the keyboard-injection
/// backends, and the SignalR broadcast stream. Bundled behind one interface
/// so <c>DictationWorker</c> depends on a single abstraction instead of the
/// four disparate services these calls previously required (#969 god-class
/// split).
/// </summary>
public interface IDictationOutputChannel
{
    /// <summary>Starts the looped "typing" sound cue that accompanies dictation.</summary>
    void StartTypingFeedback();

    /// <summary>Stops the looped "typing" sound cue. Safe to call when already stopped.</summary>
    void StopTypingFeedback();

    /// <summary>Plays the one-shot cancel earcon (used when dictation is aborted).</summary>
    void PlayCancelCue();

    /// <summary>
    /// Pastes the supplied text via the platform-native fast path (clipboard
    /// + paste shortcut), returning true if the paste succeeded.
    /// </summary>
    Task<bool> FastPasteAsync(string text, CancellationToken cancellationToken);

    /// <summary>
    /// Types the supplied text into the currently-focused window one character
    /// at a time, returning true if the injection succeeded.
    /// </summary>
    Task<bool> TypeIntoActiveWindowAsync(string text, CancellationToken cancellationToken);

    /// <summary>
    /// Broadcasts a dictation event to all SignalR clients subscribed to the
    /// dictation hub. Swallows transport errors so a client-side failure
    /// cannot take down the dictation worker.
    /// </summary>
    Task BroadcastEventAsync(DictationEventType eventType, string? text, CancellationToken cancellationToken = default);
}
