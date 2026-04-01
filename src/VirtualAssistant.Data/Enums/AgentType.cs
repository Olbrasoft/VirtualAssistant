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
    /// Gemini agent (ID: 11)
    /// </summary>
    Gemini = 11,

    /// <summary>
    /// Antigravity IDE agent (ID: 20)
    /// </summary>
    Antigravity = 20,

    /// <summary>
    /// CI/CD pipeline agent (ID: 30)
    /// </summary>
    CiPipeline = 30
}
