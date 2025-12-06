using Olbrasoft.Data.Entities.Abstractions;

namespace VirtualAssistant.Data.Entities;

/// <summary>
/// Represents a system startup record for tracking starts and shutdowns.
/// </summary>
public class SystemStartup : BaseEnity
{
    /// <summary>
    /// Gets or sets when the system started.
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets when the system shut down (null = crash or still running).
    /// </summary>
    public DateTime? ShutdownAt { get; set; }

    /// <summary>
    /// Gets or sets how the system shut down.
    /// </summary>
    public ShutdownType? ShutdownType { get; set; }

    /// <summary>
    /// Gets or sets what type of startup this was (determined at startup).
    /// </summary>
    public StartupType StartupType { get; set; }

    /// <summary>
    /// Gets or sets the greeting that was spoken (for debugging/history).
    /// </summary>
    public string? GreetingSpoken { get; set; }
}
