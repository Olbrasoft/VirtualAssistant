namespace Olbrasoft.VirtualAssistant.Service.Hubs.Services;

/// <summary>
/// Screenshot slice of the Remote Control hub (#970 split). Isolates the
/// filesystem-watch and external-script dispatch from the rest of the hub
/// so it can be unit-tested without SignalR plumbing.
/// </summary>
public interface IRemoteScreenshotCommands
{
    Task<bool> IsScreenshotAvailableAsync();
    Task InsertScreenshotPathAsync();
}
