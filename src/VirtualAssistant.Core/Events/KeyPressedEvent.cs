using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Core.Events;

/// <summary>
/// Event raised when a keyboard key is pressed.
/// </summary>
/// <param name="Key">The key code of the pressed key.</param>
public record KeyPressedEvent(KeyCode Key) : EventBase;
