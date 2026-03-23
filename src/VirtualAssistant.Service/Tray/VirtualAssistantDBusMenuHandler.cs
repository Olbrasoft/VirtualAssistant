using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Tray.Menu;
using Tmds.DBus.Protocol;
using Tmds.DBus.SourceGenerator;

namespace Olbrasoft.VirtualAssistant.Service.Tray;

/// <summary>
/// D-Bus handler for com.canonical.dbusmenu interface.
/// Provides context menu for the VirtualAssistant tray icon.
/// Facade pattern coordinating MenuStateManager, MenuLayoutBuilder, and MenuEventRouter.
/// </summary>
internal class VirtualAssistantDBusMenuHandler : ComCanonicalDbusmenuHandler, SystemTray.Linux.ITrayMenuHandler, ITrayMenuHandler, IServiceStatusUpdater, IDisposable
{
    private Connection? _connection;
    private readonly ILogger _logger;
    private PathHandler? _menuPathHandler;

    private readonly IMenuStateManager _stateManager;
    private readonly IMenuLayoutBuilder _layoutBuilder;
    private readonly IMenuEventRouter _eventRouter;

    // Event handler delegates stored for proper unsubscription
    private readonly Action _quitHandler;
    private readonly Action _muteToggleHandler;
    private readonly Action _dashboardHandler;
    private readonly Action _aboutHandler;
    private readonly Action<bool> _llmCorrectionHandler;
    private readonly Action _reloadPromptHandler;
    private readonly Action<bool> _dictationToggleHandler;
    private readonly Action<bool> _ttsMuteToggleHandler;
    private readonly Action _mercuryBillingHandler;

    /// <summary>
    /// Event fired when user selects Quit from the menu.
    /// </summary>
    public event Action? OnQuitRequested;

    /// <summary>
    /// Event fired when user selects Mute/Unmute toggle.
    /// </summary>
    public event Action? OnMuteToggleRequested;

    /// <summary>
    /// Event fired when user selects Dashboard menu item.
    /// </summary>
    public event Action? OnDashboardRequested;

    /// <summary>
    /// Event fired when user selects About menu item.
    /// </summary>
    public event Action? OnAboutRequested;

    /// <summary>
    /// Event fired when user toggles LLM correction.
    /// </summary>
    public event Action<bool>? OnLlmCorrectionToggled;

    /// <summary>
    /// Event fired when user wants to reload the Mistral prompt.
    /// </summary>
    public event Action? OnReloadPromptRequested;

    /// <summary>
    /// Event fired when user toggles dictation on/off.
    /// </summary>
    public event Action<bool>? OnDictationToggleRequested;

    /// <summary>
    /// Event fired when user toggles TTS mute on/off.
    /// </summary>
    public event Action<bool>? OnTtsMuteToggleRequested;

    /// <summary>
    /// Event fired when user selects Mercury Billing menu item.
    /// </summary>
    public event Action? OnMercuryBillingRequested;

    public VirtualAssistantDBusMenuHandler(
        ILogger<VirtualAssistantDBusMenuHandler> logger,
        IMenuStateManager stateManager,
        IMenuLayoutBuilder layoutBuilder,
        IMenuEventRouter eventRouter) : base(emitOnCapturedContext: false)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _layoutBuilder = layoutBuilder ?? throw new ArgumentNullException(nameof(layoutBuilder));
        _eventRouter = eventRouter ?? throw new ArgumentNullException(nameof(eventRouter));

        // Set D-Bus properties
        Version = 3; // dbusmenu protocol version
        TextDirection = "ltr";
        Status = "normal";
        IconThemePath = Array.Empty<string>();

        // Initialize event handler delegates
        _quitHandler = () => OnQuitRequested?.Invoke();
        _muteToggleHandler = () => OnMuteToggleRequested?.Invoke();
        _dashboardHandler = () => OnDashboardRequested?.Invoke();
        _aboutHandler = () => OnAboutRequested?.Invoke();
        _llmCorrectionHandler = (enabled) => OnLlmCorrectionToggled?.Invoke(enabled);
        _reloadPromptHandler = () => OnReloadPromptRequested?.Invoke();
        _dictationToggleHandler = (enabled) => OnDictationToggleRequested?.Invoke(enabled);
        _ttsMuteToggleHandler = (muted) => OnTtsMuteToggleRequested?.Invoke(muted);
        _mercuryBillingHandler = () => OnMercuryBillingRequested?.Invoke();

        // Subscribe to state changes to emit layout updates
        _stateManager.StateChanged += OnStateChanged;

        // Forward events from event router using stored delegates
        _eventRouter.OnQuitRequested += _quitHandler;
        _eventRouter.OnMuteToggleRequested += _muteToggleHandler;
        _eventRouter.OnDashboardRequested += _dashboardHandler;
        _eventRouter.OnAboutRequested += _aboutHandler;
        _eventRouter.OnLlmCorrectionToggled += _llmCorrectionHandler;
        _eventRouter.OnReloadPromptRequested += _reloadPromptHandler;
        _eventRouter.OnDictationToggleRequested += _dictationToggleHandler;
        _eventRouter.OnTtsMuteToggleRequested += _ttsMuteToggleHandler;
        _eventRouter.OnMercuryBillingRequested += _mercuryBillingHandler;
    }

    public override Connection Connection => _connection ?? throw new InvalidOperationException("Connection not set. Call RegisterWithDbus first.");

    /// <summary>
    /// Registers the menu handler with D-Bus connection.
    /// Creates a PathHandler in this assembly and registers itself.
    /// </summary>
    public void RegisterWithDbus(Connection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));

        _logger.LogInformation("Registering menu handler with connection {ConnectionName}", connection.UniqueName);

        // Create PathHandler in THIS assembly (VirtualAssistant.Service)
        // This avoids cross-assembly type incompatibility with PathHandler in SystemTray.Linux
        _menuPathHandler = new PathHandler("/MenuBar");
        _logger.LogDebug("Created PathHandler for /MenuBar");

        // Set the PathHandler property (types match because both are from VirtualAssistant.Service)
        PathHandler = _menuPathHandler;
        _logger.LogDebug("Set PathHandler property");

        // Add ourselves to the handler
        _menuPathHandler.Add(this);
        _logger.LogDebug("Added handler to PathHandler");

        // Register with D-Bus connection
        connection.AddMethodHandler(_menuPathHandler);
        _logger.LogInformation("Menu handler registered at /MenuBar in VirtualAssistant.Service assembly");
    }

    /// <summary>
    /// Unregisters the menu handler from D-Bus connection.
    /// </summary>
    public void UnregisterFromDbus(Connection connection)
    {
        if (_menuPathHandler is not null)
        {
            _menuPathHandler.Remove(this);
            connection.RemoveMethodHandler(_menuPathHandler.Path);
            _menuPathHandler = null;
            _logger.LogDebug("Menu handler unregistered from /MenuBar");
        }
    }

    /// <summary>
    /// Updates mute state and refreshes menu.
    /// </summary>
    public void UpdateMuteState(bool isMuted)
    {
        _stateManager.UpdateMuteState(isMuted);
    }

    /// <summary>
    /// Updates TTS mute state and refreshes menu.
    /// </summary>
    public void UpdateTtsMuteState(bool isMuted)
    {
        _stateManager.UpdateTtsMuteState(isMuted);
    }

    /// <summary>
    /// Updates log-viewer service status in menu.
    /// </summary>
    public void UpdateLogViewerStatus(bool isRunning)
    {
        _stateManager.UpdateLogViewerStatus(isRunning);
    }

    /// <summary>
    /// Updates LLM correction enabled status in menu.
    /// </summary>
    public void UpdateLlmCorrectionStatus(bool enabled)
    {
        _stateManager.UpdateLlmCorrectionStatus(enabled);
    }

    /// <summary>
    /// Updates dictation enabled status in menu.
    /// </summary>
    public void UpdateDictationStatus(bool enabled)
    {
        _stateManager.UpdateDictationStatus(enabled);
    }

    /// <summary>
    /// Handles state change events from MenuStateManager.
    /// Emits D-Bus layout update signal and item properties update for toggle items.
    /// </summary>
    private void OnStateChanged(object? sender, MenuStateChangedEventArgs e)
    {
        // Only emit if connection is established (avoid race condition during startup)
        // Use local copy to avoid race condition with UnregisterFromDbus
        var connection = _connection;
        if (connection != null)
        {
            // Emit properties update for all toggle items (dictation, LLM correction, TTS mute)
            // This notifies GNOME Shell that labels/icons have changed
            var updatedItems = new (int, Dictionary<string, VariantValue>?)[]
            {
                _layoutBuilder.GetItemProperties(MenuItemIds.DictationToggleId),
                _layoutBuilder.GetItemProperties(MenuItemIds.LlmCorrectionId),
                _layoutBuilder.GetItemProperties(MenuItemIds.TtsMuteToggleId),
                _layoutBuilder.GetItemProperties(MenuItemIds.MuteToggleId)
            };

            EmitItemsPropertiesUpdated(updatedItems, null);

            // Also emit layout update to ensure GNOME Shell refreshes the menu
            EmitLayoutUpdated(e.Revision, MenuItemIds.RootId);
        }
    }

    /// <summary>
    /// Returns the menu layout starting from the specified parent ID.
    /// </summary>
    protected override ValueTask<(uint Revision, (int, Dictionary<string, VariantValue>, VariantValue[]) Layout)> OnGetLayoutAsync(
        Message request, int parentId, int recursionDepth, string[] propertyNames)
    {
        var sender = request.Sender.Length > 0 ? System.Text.Encoding.UTF8.GetString(request.Sender) : "unknown";
        _logger.LogInformation("GetLayout called: parentId={ParentId}, depth={Depth}, connection={Sender}",
            parentId, recursionDepth, sender);

        try
        {
            var layout = _layoutBuilder.BuildMenuLayout(parentId, recursionDepth);
            _logger.LogDebug("GetLayout returning: revision={Revision}", _stateManager.Revision);
            return ValueTask.FromResult((_stateManager.Revision, layout));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetLayout FAILED");
            throw;
        }
    }

    /// <summary>
    /// Returns properties for multiple menu items.
    /// </summary>
    protected override ValueTask<(int, Dictionary<string, VariantValue>)[]> OnGetGroupPropertiesAsync(
        Message request, int[] ids, string[] propertyNames)
    {
        _logger.LogDebug("GetGroupProperties: ids=[{Ids}]", string.Join(",", ids));

        var results = ids.Select(id => _layoutBuilder.GetItemProperties(id)).ToArray();
        return ValueTask.FromResult(results);
    }

    /// <summary>
    /// Returns a single property of a menu item.
    /// </summary>
    protected override ValueTask<VariantValue> OnGetPropertyAsync(Message request, int id, string name)
    {
        _logger.LogDebug("GetProperty: id={Id}, name={Name}", id, name);

        var props = _layoutBuilder.GetItemProperties(id).Item2;
        if (props.TryGetValue(name, out var value))
        {
            return ValueTask.FromResult(value);
        }

        // Return empty string for unknown properties
        return ValueTask.FromResult(VariantValue.String(""));
    }

    /// <summary>
    /// Handles menu events (clicks).
    /// Delegates to MenuEventRouter.
    /// </summary>
    protected override ValueTask OnEventAsync(Message request, int id, string eventId, VariantValue data, uint timestamp)
    {
        _eventRouter.HandleMenuEvent(id, eventId);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Handles batch menu events.
    /// </summary>
    protected override ValueTask<int[]> OnEventGroupAsync(Message request, (int, string, VariantValue, uint)[] events)
    {
        _logger.LogDebug("EventGroup: {Count} events", events.Length);

        foreach (var (id, eventId, data, timestamp) in events)
        {
            _eventRouter.HandleMenuEvent(id, eventId);
        }

        return ValueTask.FromResult(Array.Empty<int>());
    }

    /// <summary>
    /// Disposes the handler and unsubscribes from all events to prevent memory leaks.
    /// </summary>
    public void Dispose()
    {
        // Unsubscribe from state manager events
        _stateManager.StateChanged -= OnStateChanged;

        // Unsubscribe from event router events using stored delegates
        _eventRouter.OnQuitRequested -= _quitHandler;
        _eventRouter.OnMuteToggleRequested -= _muteToggleHandler;
        _eventRouter.OnDashboardRequested -= _dashboardHandler;
        _eventRouter.OnAboutRequested -= _aboutHandler;
        _eventRouter.OnLlmCorrectionToggled -= _llmCorrectionHandler;
        _eventRouter.OnReloadPromptRequested -= _reloadPromptHandler;
        _eventRouter.OnDictationToggleRequested -= _dictationToggleHandler;
        _eventRouter.OnTtsMuteToggleRequested -= _ttsMuteToggleHandler;
        _eventRouter.OnMercuryBillingRequested -= _mercuryBillingHandler;
    }

    protected override ValueTask<bool> OnAboutToShowAsync(Message request, int id)
    {
        _logger.LogDebug("AboutToShow: id={Id}", id);
        return ValueTask.FromResult(false); // No update needed
    }

    /// <summary>
    /// Called before showing multiple menu items.
    /// </summary>
    protected override ValueTask<(int[] UpdatesNeeded, int[] IdErrors)> OnAboutToShowGroupAsync(Message request, int[] ids)
    {
        _logger.LogDebug("AboutToShowGroup: ids=[{Ids}]", string.Join(",", ids));
        return ValueTask.FromResult((Array.Empty<int>(), Array.Empty<int>()));
    }
}
