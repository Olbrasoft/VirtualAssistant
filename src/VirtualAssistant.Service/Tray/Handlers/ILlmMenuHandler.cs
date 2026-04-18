namespace Olbrasoft.VirtualAssistant.Service.Tray.Handlers;

/// <summary>
/// Handles LLM-related menu actions: correction toggle, prompt reload, Mercury
/// billing dashboard, and the transcription-corrections cache flush.
/// </summary>
public interface ILlmMenuHandler
{
    void HandleLlmCorrectionToggle(bool enabled);
    void HandleReloadPrompt();
    void HandleMercuryBilling();
    void HandleReloadCorrectionsCache();
}
