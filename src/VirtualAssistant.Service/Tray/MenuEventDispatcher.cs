using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Tray.Handlers;

namespace Olbrasoft.VirtualAssistant.Service.Tray;

/// <summary>
/// Facade over the four domain handlers that implement tray-menu actions.
/// The dispatcher itself holds no business logic — it only forwards calls so
/// that <see cref="IMenuEventDispatcher"/> stays a single entry point while
/// each domain (mute / dictation / LLM / dashboard) can evolve independently.
/// Per-handler dependencies stay inside the handler that actually uses them.
/// </summary>
public class MenuEventDispatcher : IMenuEventDispatcher
{
    private readonly IMuteMenuHandler _mute;
    private readonly IDictationMenuHandler _dictation;
    private readonly ILlmMenuHandler _llm;
    private readonly IDashboardMenuHandler _dashboard;

    public MenuEventDispatcher(
        IMuteMenuHandler mute,
        IDictationMenuHandler dictation,
        ILlmMenuHandler llm,
        IDashboardMenuHandler dashboard)
    {
        _mute = mute ?? throw new ArgumentNullException(nameof(mute));
        _dictation = dictation ?? throw new ArgumentNullException(nameof(dictation));
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
    }

    public void HandleMuteToggle() => _mute.HandleMuteToggle();
    public Task HandleTtsMuteToggleAsync(bool muted) => _mute.HandleTtsMuteToggleAsync(muted);

    public void HandleDashboard() => _dashboard.HandleDashboard();
    public void HandleAbout() => _dashboard.HandleAbout();

    public void HandleLlmCorrectionToggle(bool enabled) => _llm.HandleLlmCorrectionToggle(enabled);
    public void HandleReloadPrompt() => _llm.HandleReloadPrompt();
    public void HandleMercuryBilling() => _llm.HandleMercuryBilling();
    public void HandleReloadCorrectionsCache() => _llm.HandleReloadCorrectionsCache();

    public void HandleDictationToggle(bool enabled) => _dictation.HandleDictationToggle(enabled);
    public void HandleStreamingTranscriptionToggle(bool enabled) => _dictation.HandleStreamingTranscriptionToggle(enabled);
}
