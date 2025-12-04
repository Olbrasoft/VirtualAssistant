using System.ComponentModel;

namespace Olbrasoft.VirtualAssistant.Core.Enums;

/// <summary>
/// Type of prompt received from voice input.
/// Determines agent mode (Build/Plan) in OpenCode.
/// </summary>
public enum PromptType
{
    /// <summary>
    /// Command - action to be executed (Build mode).
    /// </summary>
    [Description("Příkaz k provedení akce")]
    Command = 1,

    /// <summary>
    /// Question - query to be answered (Plan mode).
    /// </summary>
    [Description("Otázka - jen odpověď")]
    Question = 2,

    /// <summary>
    /// Acknowledgement - statement that doesn't need a response (Plan mode).
    /// </summary>
    [Description("Konstatování")]
    Acknowledgement = 3,

    /// <summary>
    /// Confirmation - confirmation of an action (Build mode).
    /// </summary>
    [Description("Potvrzení akce")]
    Confirmation = 4,

    /// <summary>
    /// Continuation - request to continue previous action (Build mode).
    /// </summary>
    [Description("Pokračování")]
    Continuation = 5
}
