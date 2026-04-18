using Olbrasoft.VirtualAssistant.Service.Tray.Menu;
using Tmds.DBus.Protocol;
using Tmds.DBus.SourceGenerator;

namespace Olbrasoft.VirtualAssistant.Service.Tray;

/// <summary>
/// D-Bus handler for com.canonical.dbusmenu interface. Provides the context
/// menu for the VirtualAssistant tray icon.
/// After the #980 split this class keeps only the D-Bus protocol surface:
/// Connection / PathHandler registration, the overrides that answer
/// <c>GetLayout</c> / <c>GetProperty</c> / <c>Event*</c> calls, and the
/// <c>OnStateChanged</c> hook that turns <see cref="IMenuStateManager"/>
/// state changes into <c>EmitLayoutUpdated</c> / <c>EmitItemsPropertiesUpdated</c>
/// D-Bus signals. Application-level menu state updates go to
/// <see cref="IMenuStateManager"/> directly (via <see cref="Core.Services.IServiceStatusUpdater"/>),
/// and application-level menu clicks come out of
/// <see cref="IMenuEventForwarder"/> — neither needs to touch this class.
/// </summary>
internal class VirtualAssistantDBusMenuHandler : ComCanonicalDbusmenuHandler, SystemTray.Linux.ITrayMenuHandler, Core.Services.ITrayMenuHandler, IDisposable
{
    private readonly ILogger _logger;
    private readonly IMenuStateManager _stateManager;
    private readonly IMenuLayoutBuilder _layoutBuilder;
    private readonly IMenuEventRouter _eventRouter;
    private Connection? _connection;
    private PathHandler? _menuPathHandler;

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

        Version = 3; // dbusmenu protocol version
        TextDirection = "ltr";
        Status = "normal";
        IconThemePath = Array.Empty<string>();

        _stateManager.StateChanged += OnStateChanged;
    }

    public override Connection Connection => _connection ?? throw new InvalidOperationException("Connection not set. Call RegisterWithDbus first.");

    /// <summary>
    /// Registers the menu handler with a D-Bus connection. Creates a PathHandler
    /// in this assembly and registers itself on it.
    /// </summary>
    public void RegisterWithDbus(Connection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _logger.LogInformation("Registering menu handler with connection {ConnectionName}", connection.UniqueName);

        // PathHandler lives in this assembly to avoid cross-assembly type
        // incompatibility with the PathHandler exported by SystemTray.Linux.
        _menuPathHandler = new PathHandler("/MenuBar");
        PathHandler = _menuPathHandler;
        _menuPathHandler.Add(this);
        connection.AddMethodHandler(_menuPathHandler);

        _logger.LogInformation("Menu handler registered at /MenuBar");
    }

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
    /// Bridges state changes to the D-Bus signals GNOME Shell listens for.
    /// </summary>
    private void OnStateChanged(object? sender, MenuStateChangedEventArgs e)
    {
        var connection = _connection;
        if (connection is null) return;

        // Emit properties update for toggle items so GNOME Shell refreshes
        // labels/icons without rebuilding the whole layout.
        var updatedItems = new (int, Dictionary<string, VariantValue>?)[]
        {
            _layoutBuilder.GetItemProperties(MenuItemIds.DictationToggleId),
            _layoutBuilder.GetItemProperties(MenuItemIds.LlmCorrectionId),
            _layoutBuilder.GetItemProperties(MenuItemIds.TtsMuteToggleId),
            _layoutBuilder.GetItemProperties(MenuItemIds.MuteToggleId),
        };
        EmitItemsPropertiesUpdated(updatedItems, null);

        // Layout update ensures GNOME Shell re-fetches the menu structure.
        EmitLayoutUpdated(e.Revision, MenuItemIds.RootId);
    }

    protected override ValueTask<(uint Revision, (int, Dictionary<string, VariantValue>, VariantValue[]) Layout)> OnGetLayoutAsync(
        Message request, int parentId, int recursionDepth, string[] propertyNames)
    {
        var sender = request.Sender.Length > 0 ? System.Text.Encoding.UTF8.GetString(request.Sender) : "unknown";
        _logger.LogInformation("GetLayout called: parentId={ParentId}, depth={Depth}, connection={Sender}",
            parentId, recursionDepth, sender);

        try
        {
            var layout = _layoutBuilder.BuildMenuLayout(parentId, recursionDepth);
            return ValueTask.FromResult((_stateManager.Revision, layout));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetLayout FAILED");
            throw;
        }
    }

    protected override ValueTask<(int, Dictionary<string, VariantValue>)[]> OnGetGroupPropertiesAsync(
        Message request, int[] ids, string[] propertyNames)
    {
        _logger.LogDebug("GetGroupProperties: ids=[{Ids}]", string.Join(",", ids));
        var results = ids.Select(id => _layoutBuilder.GetItemProperties(id)).ToArray();
        return ValueTask.FromResult(results);
    }

    protected override ValueTask<VariantValue> OnGetPropertyAsync(Message request, int id, string name)
    {
        _logger.LogDebug("GetProperty: id={Id}, name={Name}", id, name);
        var props = _layoutBuilder.GetItemProperties(id).Item2;
        return ValueTask.FromResult(props.TryGetValue(name, out var value) ? value : VariantValue.String(""));
    }

    protected override ValueTask OnEventAsync(Message request, int id, string eventId, VariantValue data, uint timestamp)
    {
        _eventRouter.HandleMenuEvent(id, eventId);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<int[]> OnEventGroupAsync(Message request, (int, string, VariantValue, uint)[] events)
    {
        _logger.LogDebug("EventGroup: {Count} events", events.Length);
        foreach (var (id, eventId, _, _) in events)
            _eventRouter.HandleMenuEvent(id, eventId);
        return ValueTask.FromResult(Array.Empty<int>());
    }

    protected override ValueTask<bool> OnAboutToShowAsync(Message request, int id)
    {
        _logger.LogDebug("AboutToShow: id={Id}", id);
        return ValueTask.FromResult(false);
    }

    protected override ValueTask<(int[] UpdatesNeeded, int[] IdErrors)> OnAboutToShowGroupAsync(Message request, int[] ids)
    {
        _logger.LogDebug("AboutToShowGroup: ids=[{Ids}]", string.Join(",", ids));
        return ValueTask.FromResult((Array.Empty<int>(), Array.Empty<int>()));
    }

    public void Dispose()
    {
        _stateManager.StateChanged -= OnStateChanged;
    }
}
