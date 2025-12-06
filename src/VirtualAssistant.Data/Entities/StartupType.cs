namespace VirtualAssistant.Data.Entities;

/// <summary>
/// Represents the type of system startup.
/// </summary>
public enum StartupType
{
    /// <summary>
    /// Normal startup.
    /// </summary>
    Normal = 0,

    /// <summary>
    /// Startup after a crash.
    /// </summary>
    AfterCrash = 1,

    /// <summary>
    /// Frequent restart detected.
    /// </summary>
    FrequentRestart = 2,

    /// <summary>
    /// Development mode startup.
    /// </summary>
    Development = 3
}
