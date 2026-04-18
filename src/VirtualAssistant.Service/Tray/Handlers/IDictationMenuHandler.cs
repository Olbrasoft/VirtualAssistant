namespace Olbrasoft.VirtualAssistant.Service.Tray.Handlers;

/// <summary>
/// Handles dictation-related menu actions. Dictation control is optional
/// (not every deployment runs dictation), so missing control services are
/// logged and ignored rather than crashing the menu.
/// </summary>
public interface IDictationMenuHandler
{
    void HandleDictationToggle(bool enabled);
    void HandleStreamingTranscriptionToggle(bool enabled);
}
