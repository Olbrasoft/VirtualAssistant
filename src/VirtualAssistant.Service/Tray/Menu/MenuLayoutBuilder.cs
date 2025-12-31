using Tmds.DBus.Protocol;

namespace Olbrasoft.VirtualAssistant.Service.Tray.Menu;

/// <summary>
/// Builds D-Bus menu layout structures based on current menu state.
/// Creates menu item hierarchies, properties, and labels.
/// </summary>
public class MenuLayoutBuilder : IMenuLayoutBuilder
{
    private readonly IMenuStateManager _stateManager;

    public MenuLayoutBuilder(IMenuStateManager stateManager)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
    }

    /// <summary>
    /// Builds the complete menu layout starting from the specified parent ID.
    /// </summary>
    public (int, Dictionary<string, VariantValue>, VariantValue[]) BuildMenuLayout(int parentId, int recursionDepth)
    {
        if (parentId == MenuItemIds.RootId)
        {
            return BuildRootLayout(recursionDepth);
        }

        return GetMenuItemLayout(parentId);
    }

    /// <summary>
    /// Gets properties for a specific menu item.
    /// </summary>
    public (int, Dictionary<string, VariantValue>) GetItemProperties(int id)
    {
        // Compute labels lazily only when needed for the specific menu item
        return id switch
        {
            MenuItemIds.RootId => (id, new Dictionary<string, VariantValue>
            {
                ["children-display"] = VariantValue.String("submenu")
            }),
            MenuItemIds.StatusId => (id, new Dictionary<string, VariantValue>
            {
                ["label"] = VariantValue.String("VirtualAssistant - poslouchám"),
                ["enabled"] = VariantValue.Bool(false),
                ["visible"] = VariantValue.Bool(true)
            }),
            MenuItemIds.Separator1Id or MenuItemIds.Separator2Id or MenuItemIds.Separator3Id or MenuItemIds.Separator4Id => (id, new Dictionary<string, VariantValue>
            {
                ["type"] = VariantValue.String("separator"),
                ["visible"] = VariantValue.Bool(true)
            }),
            MenuItemIds.DictationToggleId => (id, new Dictionary<string, VariantValue>
            {
                ["label"] = VariantValue.String(GetDictationLabel()),
                ["enabled"] = VariantValue.Bool(true),
                ["visible"] = VariantValue.Bool(true)
            }),
            MenuItemIds.LogViewerId => (id, new Dictionary<string, VariantValue>
            {
                ["label"] = VariantValue.String(GetLogViewerLabel()),
                ["enabled"] = VariantValue.Bool(true),
                ["visible"] = VariantValue.Bool(true)
            }),
            MenuItemIds.LlmCorrectionId => (id, new Dictionary<string, VariantValue>
            {
                ["label"] = VariantValue.String(GetLlmCorrectionLabel()),
                ["enabled"] = VariantValue.Bool(true),
                ["visible"] = VariantValue.Bool(true)
            }),
            MenuItemIds.ReloadPromptId => (id, new Dictionary<string, VariantValue>
            {
                ["label"] = VariantValue.String("🔄 Reload LLM Prompt"),
                ["enabled"] = VariantValue.Bool(true),
                ["visible"] = VariantValue.Bool(true)
            }),
            MenuItemIds.MuteToggleId => (id, new Dictionary<string, VariantValue>
            {
                ["label"] = VariantValue.String(GetMuteLabel()),
                ["enabled"] = VariantValue.Bool(true),
                ["visible"] = VariantValue.Bool(true)
            }),
            MenuItemIds.TtsMuteToggleId => (id, new Dictionary<string, VariantValue>
            {
                ["label"] = VariantValue.String(GetTtsMuteLabel()),
                ["enabled"] = VariantValue.Bool(true),
                ["visible"] = VariantValue.Bool(true)
            }),
            MenuItemIds.ShowLogsId => (id, new Dictionary<string, VariantValue>
            {
                ["label"] = VariantValue.String("Zobrazit logy"),
                ["enabled"] = VariantValue.Bool(true),
                ["visible"] = VariantValue.Bool(true)
            }),
            MenuItemIds.QuitId => (id, new Dictionary<string, VariantValue>
            {
                ["label"] = VariantValue.String("Ukončit"),
                ["enabled"] = VariantValue.Bool(true),
                ["visible"] = VariantValue.Bool(true)
            }),
            _ => (id, new Dictionary<string, VariantValue>())
        };
    }

    private (int, Dictionary<string, VariantValue>, VariantValue[]) BuildRootLayout(int recursionDepth)
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
            children = System.Array.Empty<VariantValue>();
        }
        else
        {
            var muteLabel = GetMuteLabel();
            var ttsMuteLabel = GetTtsMuteLabel();
            var dictationLabel = GetDictationLabel();
            var logViewerLabel = GetLogViewerLabel();
            var llmCorrectionLabel = GetLlmCorrectionLabel();

            children = new VariantValue[]
            {
                CreateChildVariant(MenuItemIds.StatusId, "VirtualAssistant - poslouchám", false, enabled: false),
                CreateChildVariant(MenuItemIds.Separator1Id, "", true),
                CreateChildVariant(MenuItemIds.DictationToggleId, dictationLabel, false),
                CreateChildVariant(MenuItemIds.Separator2Id, "", true),
                CreateChildVariant(MenuItemIds.LlmCorrectionId, llmCorrectionLabel, false),
                CreateChildVariant(MenuItemIds.ReloadPromptId, "🔄 Reload LLM Prompt", false),
                CreateChildVariant(MenuItemIds.Separator3Id, "", true),
                CreateChildVariant(MenuItemIds.MuteToggleId, muteLabel, false),
                CreateChildVariant(MenuItemIds.TtsMuteToggleId, ttsMuteLabel, false),
                CreateChildVariant(MenuItemIds.ShowLogsId, "Zobrazit logy", false),
                CreateChildVariant(MenuItemIds.LogViewerId, logViewerLabel, false),
                CreateChildVariant(MenuItemIds.Separator4Id, "", true),
                CreateChildVariant(MenuItemIds.QuitId, "Ukončit", false)
            };
        }

        return (MenuItemIds.RootId, rootProps, children);
    }

    private (int, Dictionary<string, VariantValue>, VariantValue[]) GetMenuItemLayout(int id)
    {
        // Reuse GetItemProperties to avoid code duplication
        var (itemId, props) = GetItemProperties(id);
        return (itemId, props, Array.Empty<VariantValue>());
    }

    private VariantValue CreateChildVariant(int id, string label, bool isSeparator, bool enabled = true)
    {
        // Create a struct variant for menu item: (ia{sv}av)
        var props = new Dictionary<string, VariantValue>();
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
        var children = Array.Empty<VariantValue>();

        // Create the struct (ia{sv}av)
        return Struct.Create(id, props, children);
    }

    private string GetMuteLabel()
    {
        return _stateManager.IsMuted ? "🔊 Zapnout mikrofon" : "🔇 Ztlumit mikrofon";
    }

    private string GetTtsMuteLabel()
    {
        return _stateManager.IsTtsMuted ? "❌ TextToSpeech - Zapnout" : "✅ TextToSpeech - Stlumit";
    }

    private string GetDictationLabel()
    {
        return _stateManager.IsDictationEnabled
            ? "✅ Diktace - Vypnout"
            : "❌ Diktace - Zapnout";
    }

    private string GetLogViewerLabel()
    {
        return _stateManager.LogViewerStatus == "Running"
            ? "✅ Log Viewer - Vypnout"
            : "❌ Log Viewer - Zapnout";
    }

    private string GetLlmCorrectionLabel()
    {
        return _stateManager.IsLlmCorrectionEnabled
            ? "✅ Posílání do LLM - Vypnout"
            : "❌ Posílání do LLM - Zapnout";
    }
}
