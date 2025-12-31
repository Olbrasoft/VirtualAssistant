using Tmds.DBus.Protocol;

namespace Olbrasoft.VirtualAssistant.Service.Tray.Menu;

/// <summary>
/// Builds D-Bus menu layout structures based on current menu state.
/// Responsible for creating menu item hierarchies and properties.
/// </summary>
public interface IMenuLayoutBuilder
{
    /// <summary>
    /// Builds the complete menu layout starting from the specified parent ID.
    /// Returns a tuple of (itemId, properties, children).
    /// </summary>
    (int, Dictionary<string, VariantValue>, VariantValue[]) BuildMenuLayout(int parentId, int recursionDepth);

    /// <summary>
    /// Gets properties for a specific menu item.
    /// Returns a tuple of (itemId, properties).
    /// </summary>
    (int, Dictionary<string, VariantValue>) GetItemProperties(int id);
}
