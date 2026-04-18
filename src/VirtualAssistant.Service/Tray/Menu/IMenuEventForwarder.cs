namespace Olbrasoft.VirtualAssistant.Service.Tray.Menu;

/// <summary>
/// Publishes the menu-click events that reach the tray handler from D-Bus
/// (via <see cref="IMenuEventRouter"/>) to application-level subscribers.
/// Extracted from <c>VirtualAssistantDBusMenuHandler</c> during the #980
/// split so the D-Bus handler can focus on protocol methods and the
/// wire-up stays in one place.
/// </summary>
public interface IMenuEventForwarder : IDisposable
{
    event Action? OnQuitRequested;
    event Action? OnMuteToggleRequested;
    event Action? OnDashboardRequested;
    event Action? OnAboutRequested;
    event Action<bool>? OnLlmCorrectionToggled;
    event Action? OnReloadPromptRequested;
    event Action<bool>? OnDictationToggleRequested;
    event Action<bool>? OnTtsMuteToggleRequested;
    event Action? OnMercuryBillingRequested;
    event Action<bool>? OnStreamingTranscriptionToggled;
    event Action? OnReloadCorrectionsCacheRequested;
}
