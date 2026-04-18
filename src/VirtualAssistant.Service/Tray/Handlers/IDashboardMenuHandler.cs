namespace Olbrasoft.VirtualAssistant.Service.Tray.Handlers;

/// <summary>
/// Handles browser / dialog menu actions (Dashboard open, About dialog).
/// Kept separate from LLM/billing so the dashboard-URL configuration lives
/// with only the handlers that actually need it.
/// </summary>
public interface IDashboardMenuHandler
{
    void HandleDashboard();
    void HandleAbout();
}
