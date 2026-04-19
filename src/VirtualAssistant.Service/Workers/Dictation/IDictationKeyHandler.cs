using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.StateMachine;

namespace Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

/// <summary>
/// Keyboard-event decision tree lifted out of <c>DictationWorker</c>
/// (#969 god-class split). Owns the <see cref="IKeyboardMonitor"/>
/// subscription + the ScrollLock/Pause routing logic so the worker
/// never has to see raw key events — it just plugs in the four state
/// transitions the handler can trigger via <see cref="Start"/>.
/// </summary>
public interface IDictationKeyHandler
{
    /// <summary>
    /// Subscribe to keyboard events and wire the four actions the handler
    /// can trigger (start recording, stop + transcribe, cancel while
    /// recording, cancel during transcription). The providers give the
    /// handler the runtime state it needs to route each key release.
    /// Call <see cref="Stop"/> on worker shutdown to unsubscribe.
    /// </summary>
    void Start(IDictationKeyHandlerBindings bindings);

    /// <summary>
    /// Unsubscribe from keyboard events; safe to call if <see cref="Start"/>
    /// was never called.
    /// </summary>
    void Stop();
}

/// <summary>
/// State + action surface the key handler calls back into. Implemented by
/// <c>DictationWorker</c> (via a tiny adapter) so the handler never
/// references the worker directly.
/// </summary>
public interface IDictationKeyHandlerBindings
{
    /// <summary>Whether dictation is globally enabled — a false short-circuits every key.</summary>
    bool IsEnabled { get; }

    /// <summary>Current state-machine state used to route the key.</summary>
    DictationState State { get; }

    /// <summary>Start a normal-mode recording session (ScrollLock from Idle).</summary>
    Task StartAsync();

    /// <summary>Stop recording and run the full completion pipeline (ScrollLock from Recording).</summary>
    Task StopAndTranscribeAsync();

    /// <summary>Cancel an in-flight recording; discards audio (Pause from Recording).</summary>
    Task CancelRecordingAsync();

    /// <summary>Cancel a running transcription (Pause from Transcribing).</summary>
    void CancelTranscription();
}
