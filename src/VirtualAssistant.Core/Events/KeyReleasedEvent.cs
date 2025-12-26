using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Core.Events;

/// <summary>
/// Event raised when a keyboard key is released.
/// </summary>
/// <param name="Key">The key code of the released key.</param>
public record KeyReleasedEvent(KeyCode Key) : EventBase;
