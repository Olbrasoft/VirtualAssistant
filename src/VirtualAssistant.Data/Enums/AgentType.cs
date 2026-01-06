namespace Olbrasoft.VirtualAssistant.Data.Enums;

/// <summary>
/// Defines the types of agents that can send notifications to the system.
/// Values correspond to agent IDs in the database.
/// </summary>
public enum AgentType
{
    /// <summary>
    /// OpenCode agent (ID: 1)
    /// </summary>
    OpenCode = 1,

    /// <summary>
    /// Claude Code agent (ID: 4)
    /// </summary>
    ClaudeCode = 4,

    /// <summary>
    /// Gemini agent (ID: 11) - will be added to database
    /// </summary>
    Gemini = 11
}
