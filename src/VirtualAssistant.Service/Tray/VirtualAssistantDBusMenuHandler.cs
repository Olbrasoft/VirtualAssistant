using Microsoft.Extensions.Logging;
using Olbrasoft.SystemTray.Linux;
using Olbrasoft.VirtualAssistant.Core.Exceptions;
using Tmds.DBus.Protocol;
using Tmds.DBus.SourceGenerator;

namespace Olbrasoft.VirtualAssistant.Service.Tray;

/// <summary>
/// D-Bus handler for com.canonical.dbusmenu interface.
/// Provides context menu for the VirtualAssistant tray icon.
/// </summary>
internal class VirtualAssistantDBusMenuHandler : ComCanonicalDbusmenuHandler, ITrayMenuHandler
{
    private Connection? _connection;
    private readonly ILogger _logger;
    private uint _revision = 1;
    private PathHandler? _menuPathHandler;

    // Menu item IDs
    private const int RootId = 0;
    private const int StatusId = 1;
    private const int Separator1Id = 2;
    private const int MuteToggleId = 3;
    private const int ShowLogsId = 4;
    private const int Separator2Id = 5;
    private const int QuitId = 6;

    /// <summary>
    /// Event fired when user selects Quit from the menu.
    /// </summary>
    public event Action? OnQuitRequested;

    /// <summary>
    /// Event fired when user selects Mute/Unmute toggle.
    /// </summary>
    public event Action? OnMuteToggleRequested;

    /// <summary>
    /// Event fired when user selects Show Logs.
    /// </summary>
    public event Action? OnShowLogsRequested;

    private bool _isMuted;

    public VirtualAssistantDBusMenuHandler(ILogger logger) : base(emitOnCapturedContext: false)
    {
        _logger = logger;

        // Set D-Bus properties
        Version = 3; // dbusmenu protocol version
        TextDirection = "ltr";
        Status = "normal";
        IconThemePath = Array.Empty<string>();
    }

    public override Connection Connection => _connection ?? throw new TrayServiceException("D-Bus connection not set. Call RegisterWithDbus first.");

    /// <summary>
    /// Registers the menu handler with D-Bus connection.
    /// Creates a PathHandler in this assembly and registers itself.
    /// </summary>
    public void RegisterWithDbus(Connection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));

        // Create PathHandler in THIS assembly (VirtualAssistant.Service)
        // This avoids cross-assembly type incompatibility with PathHandler in SystemTray.Linux
        _menuPathHandler = new PathHandler("/MenuBar");

        // Set the PathHandler property (types match because both are from VirtualAssistant.Service)
        PathHandler = _menuPathHandler;

        // Add ourselves to the handler
        _menuPathHandler.Add(this);

        // Register with D-Bus connection
        connection.AddMethodHandler(_menuPathHandler);

        _logger.LogDebug("Menu handler registered at /MenuBar in VirtualAssistant.Service assembly");
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
        _isMuted = isMuted;
        _revision++;

        // Emit LayoutUpdated signal to notify menu changed
        EmitLayoutUpdated(_revision, RootId);
    }

    /// <summary>
    /// Returns the menu layout starting from the specified parent ID.
    /// </summary>
    protected override ValueTask<(uint Revision, (int, Dictionary<string, VariantValue>, VariantValue[]) Layout)> OnGetLayoutAsync(
        Message request, int parentId, int recursionDepth, string[] propertyNames)
    {
        _logger.LogDebug("GetLayout: parentId={ParentId}, depth={Depth}", parentId, recursionDepth);

        var layout = BuildMenuLayout(parentId, recursionDepth);
        return ValueTask.FromResult((_revision, layout));
    }

    private (int, Dictionary<string, VariantValue>, VariantValue[]) BuildMenuLayout(int parentId, int recursionDepth)
    {
        if (parentId == RootId)
        {
            // Root menu with children
            var rootProps = new Dictionary<string, VariantValue>
            {
                ["children-display"] = VariantValue.String("submenu")
            };

            // Build child menu items
            VariantValue[] children;
            if (recursionDepth == 0)
            {
                children = Array.Empty<VariantValue>();
            }
            else
            {
                var muteLabel = _isMuted ? "🔊 Zapnout mikrofon" : "🔇 Ztlumit mikrofon";
                children = new VariantValue[]
                {
                    CreateChildVariant(StatusId, "VirtualAssistant - poslouchám", false, enabled: false),
                    CreateChildVariant(Separator1Id, "", true),
                    CreateChildVariant(MuteToggleId, muteLabel, false),
                    CreateChildVariant(ShowLogsId, "Zobrazit logy", false),
                    CreateChildVariant(Separator2Id, "", true),
                    CreateChildVariant(QuitId, "Ukončit", false)
                };
            }

            return (RootId, rootProps, children);
        }

        // For non-root items, return the specific item
        return GetMenuItemLayout(parentId);
    }

    private VariantValue CreateChildVariant(int id, string label, bool isSeparator, bool enabled = true)
    {
        // Create a struct variant for menu item: (ia{sv}av)
        var props = new Dict<string, VariantValue>();
        if (isSeparator)
        {
            props.Add("type", VariantValue.String("separator"));
            props.Add("visible", VariantValue.Bool(true));
        }
        else
        {
            props.Add("label", VariantValue.String(label));
            props.Add("enabled", VariantValue.Bool(enabled));
            props.Add("visible", VariantValue.Bool(true));
        }

        // Empty children array for leaf items
        var children = new Array<VariantValue>();

        // Create the struct (ia{sv}av)
        return Struct.Create(id, props, children);
    }

    private (int, Dictionary<string, VariantValue>, VariantValue[]) GetMenuItemLayout(int id)
    {
        var props = new Dictionary<string, VariantValue>();

        switch (id)
        {
            case StatusId:
                props["label"] = VariantValue.String("VirtualAssistant - poslouchám");
                props["enabled"] = VariantValue.Bool(false);
                props["visible"] = VariantValue.Bool(true);
                break;
            case Separator1Id:
            case Separator2Id:
                props["type"] = VariantValue.String("separator");
                props["visible"] = VariantValue.Bool(true);
                break;
            case MuteToggleId:
                var muteLabel = _isMuted ? "🔊 Zapnout mikrofon" : "🔇 Ztlumit mikrofon";
                props["label"] = VariantValue.String(muteLabel);
                props["enabled"] = VariantValue.Bool(true);
                props["visible"] = VariantValue.Bool(true);
                break;
            case ShowLogsId:
                props["label"] = VariantValue.String("Zobrazit logy");
                props["enabled"] = VariantValue.Bool(true);
                props["visible"] = VariantValue.Bool(true);
                break;
            case QuitId:
                props["label"] = VariantValue.String("Ukončit");
                props["enabled"] = VariantValue.Bool(true);
                props["visible"] = VariantValue.Bool(true);
                break;
        }

        return (id, props, Array.Empty<VariantValue>());
    }

    /// <summary>
    /// Returns properties for multiple menu items.
    /// </summary>
    protected override ValueTask<(int, Dictionary<string, VariantValue>)[]> OnGetGroupPropertiesAsync(
        Message request, int[] ids, string[] propertyNames)
    {
        _logger.LogDebug("GetGroupProperties: ids=[{Ids}]", string.Join(",", ids));

        var results = ids.Select(id => GetItemProperties(id)).ToArray();
        return ValueTask.FromResult(results);
    }

    private (int, Dictionary<string, VariantValue>) GetItemProperties(int id)
    {
        var muteLabel = _isMuted ? "🔊 Zapnout mikrofon" : "🔇 Ztlumit mikrofon";

        return id switch
        {
            RootId => (id, new Dictionary<string, VariantValue>
            {
                ["children-display"] = VariantValue.String("submenu")
            }),
            StatusId => (id, new Dictionary<string, VariantValue>
            {
                ["label"] = VariantValue.String("VirtualAssistant - poslouchám"),
                ["enabled"] = VariantValue.Bool(false),
                ["visible"] = VariantValue.Bool(true)
            }),
            Separator1Id or Separator2Id => (id, new Dictionary<string, VariantValue>
            {
                ["type"] = VariantValue.String("separator"),
                ["visible"] = VariantValue.Bool(true)
            }),
            MuteToggleId => (id, new Dictionary<string, VariantValue>
            {
                ["label"] = VariantValue.String(muteLabel),
                ["enabled"] = VariantValue.Bool(true),
                ["visible"] = VariantValue.Bool(true)
            }),
            ShowLogsId => (id, new Dictionary<string, VariantValue>
            {
                ["label"] = VariantValue.String("Zobrazit logy"),
                ["enabled"] = VariantValue.Bool(true),
                ["visible"] = VariantValue.Bool(true)
            }),
            QuitId => (id, new Dictionary<string, VariantValue>
            {
                ["label"] = VariantValue.String("Ukončit"),
                ["enabled"] = VariantValue.Bool(true),
                ["visible"] = VariantValue.Bool(true)
            }),
            _ => (id, new Dictionary<string, VariantValue>())
        };
    }

    /// <summary>
    /// Returns a single property of a menu item.
    /// </summary>
    protected override ValueTask<VariantValue> OnGetPropertyAsync(Message request, int id, string name)
    {
        _logger.LogDebug("GetProperty: id={Id}, name={Name}", id, name);

        var props = GetItemProperties(id).Item2;
        if (props.TryGetValue(name, out var value))
        {
            return ValueTask.FromResult(value);
        }

        // Return empty string for unknown properties
        return ValueTask.FromResult(VariantValue.String(""));
    }

    /// <summary>
    /// Handles menu events (clicks).
    /// </summary>
    protected override ValueTask OnEventAsync(Message request, int id, string eventId, VariantValue data, uint timestamp)
    {
        _logger.LogDebug("Event: id={Id}, eventId={EventId}", id, eventId);

        if (eventId == "clicked")
        {
            switch (id)
            {
                case QuitId:
                    _logger.LogInformation("Quit menu item clicked");
                    OnQuitRequested?.Invoke();
                    break;
                case MuteToggleId:
                    _logger.LogInformation("Mute toggle menu item clicked");
                    OnMuteToggleRequested?.Invoke();
                    break;
                case ShowLogsId:
                    _logger.LogInformation("Show logs menu item clicked");
                    OnShowLogsRequested?.Invoke();
                    break;
            }
        }

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
            _ = OnEventAsync(request, id, eventId, data, timestamp);
        }

        return ValueTask.FromResult(Array.Empty<int>());
    }

    /// <summary>
    /// Called before showing a menu item. Returns whether the menu needs update.
    /// </summary>
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
