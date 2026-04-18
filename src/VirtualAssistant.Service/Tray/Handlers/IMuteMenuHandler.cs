namespace Olbrasoft.VirtualAssistant.Service.Tray.Handlers;

/// <summary>
/// Handles the mute-related menu actions (microphone toggle, TTS mute).
/// </summary>
public interface IMuteMenuHandler
{
    void HandleMuteToggle();
    Task HandleTtsMuteToggleAsync(bool muted);
}
