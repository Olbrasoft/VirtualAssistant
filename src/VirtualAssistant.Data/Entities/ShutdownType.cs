namespace VirtualAssistant.Data.Entities;

/// <summary>
/// Represents how the system was shut down.
/// </summary>
public enum ShutdownType
{
    /// <summary>
    /// Unknown shutdown reason.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Clean shutdown.
    /// </summary>
    Clean = 1,

    /// <summary>
    /// Crash or unexpected shutdown.
    /// </summary>
    Crash = 2
}
