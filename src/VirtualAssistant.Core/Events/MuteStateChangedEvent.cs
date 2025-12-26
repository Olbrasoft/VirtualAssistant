namespace Olbrasoft.VirtualAssistant.Core.Events;

/// <summary>
/// Event raised when microphone mute state changes.
/// </summary>
/// <param name="IsMuted">True if microphone is now muted, false if unmuted.</param>
public record MuteStateChangedEvent(bool IsMuted) : EventBase;
