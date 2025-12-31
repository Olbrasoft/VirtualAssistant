namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Coordinates tray icon management for left hand, center (VirtualAssistant), and right hand icons.
/// Implements Single Responsibility Principle - only handles icon coordination.
/// </summary>
public interface ITrayIconCoordinator
{
    /// <summary>
    /// Initializes all three tray icons (left hand, center, right hand).
    /// </summary>
    Task InitializeIconsAsync();

    /// <summary>
    /// Updates the center icon based on mute state.
    /// </summary>
    /// <param name="isMuted">Whether the assistant is muted</param>
    void UpdateCenterIcon(bool isMuted);

    /// <summary>
    /// Sets the left hand icon to the specified icon file.
    /// </summary>
    /// <param name="iconFileName">Icon file name (e.g., "default-left-hand.svg")</param>
    void SetLeftHandIcon(string iconFileName);

    /// <summary>
    /// Sets the right hand icon to the specified icon file.
    /// </summary>
    /// <param name="iconFileName">Icon file name (e.g., "default-right-hand.svg")</param>
    void SetRightHandIcon(string iconFileName);

    /// <summary>
    /// Sets the center head icon to the specified icon file.
    /// </summary>
    /// <param name="iconFileName">Icon file name (e.g., "default-head.svg")</param>
    void SetCenterIcon(string iconFileName);
}
