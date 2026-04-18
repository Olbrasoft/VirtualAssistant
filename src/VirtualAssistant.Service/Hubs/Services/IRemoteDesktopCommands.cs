using Olbrasoft.VirtualAssistant.Service.Hubs;

namespace Olbrasoft.VirtualAssistant.Service.Hubs.Services;

/// <summary>
/// Window / workspace / app-launch slice of the Remote Control hub (#970
/// split). All desktop-level operations that don't involve the dictation
/// flow live here — focused domain for D-Bus window calls and launcher
/// fallbacks.
/// </summary>
public interface IRemoteDesktopCommands
{
    Task<string> GetFocusedAppAsync();
    Task<bool> CloseAppAsync(string wmClass);
    Task<WorkspaceInfo> GetWorkspaceInfoAsync();
    Task SwitchWorkspaceAsync(int workspaceNumber);
    Task<bool> ActivateAppAsync(string wmClass);
}
