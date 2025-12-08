namespace Olbrasoft.VirtualAssistant.Core.Enums;

/// <summary>
/// Possible actions from LLM routing.
/// </summary>
public enum LlmRouterAction
{
    /// <summary>Send command to OpenCode.</summary>
    OpenCode,

    /// <summary>Respond directly via TTS.</summary>
    Respond,

    /// <summary>Execute bash command (redirected to OpenCode).</summary>
    Bash,

    /// <summary>Save a note.</summary>
    SaveNote,

    /// <summary>Start a discussion/planning session (persistent PLAN mode).</summary>
    StartDiscussion,

    /// <summary>End a discussion/planning session (return to normal mode).</summary>
    EndDiscussion,

    /// <summary>Dispatch a task to an agent (e.g., Claude).</summary>
    DispatchTask,

    /// <summary>Ignore the input.</summary>
    Ignore
}
