using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.SystemTray.Linux;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Tray;

namespace Olbrasoft.VirtualAssistant.Service.Factories;

/// <summary>
/// Factory for creating VirtualAssistantTrayService instances with all dependencies.
/// Follows Factory Method pattern to encapsulate complex object creation.
/// </summary>
public class VirtualAssistantTrayServiceFactory
{
    private readonly ILogger<VirtualAssistantTrayService> _logger;
    private readonly TrayIconManager _manager;
    private readonly IManualMuteService _muteService;
    private readonly ITrayMenuHandler _menuHandler;
    private readonly IOptions<ContinuousListenerOptions> _listenerOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualAssistantTrayServiceFactory"/> class.
    /// </summary>
    public VirtualAssistantTrayServiceFactory(
        ILogger<VirtualAssistantTrayService> logger,
        TrayIconManager manager,
        IManualMuteService muteService,
        ITrayMenuHandler menuHandler,
        IOptions<ContinuousListenerOptions> listenerOptions)
    {
        _logger = logger;
        _manager = manager;
        _muteService = muteService;
        _menuHandler = menuHandler;
        _listenerOptions = listenerOptions;
    }

    /// <summary>
    /// Creates a new VirtualAssistantTrayService instance.
    /// </summary>
    /// <returns>Configured VirtualAssistantTrayService instance.</returns>
    public VirtualAssistantTrayService Create()
    {
        var iconsPath = Path.Combine(AppContext.BaseDirectory, "icons");

        return new VirtualAssistantTrayService(
            _logger,
            _manager,
            _muteService,
            iconsPath,
            _listenerOptions.Value.LogViewerPort,
            _menuHandler);
    }
}
