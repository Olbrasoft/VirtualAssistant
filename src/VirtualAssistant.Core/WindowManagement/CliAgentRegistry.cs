namespace Olbrasoft.VirtualAssistant.Core.WindowManagement;

/// <summary>
/// A CLI agent we know how to detect, plus everything the three detection
/// strategies need: the process name we'd find in the terminal's descendants,
/// the substrings the TUI sets in the terminal title, and the prefix the
/// user's tmux wrapper gives to sessions it spawns for this agent. Keeping
/// these together means adding a new agent is a one-line change — there's
/// no risk of the three detection paths drifting out of sync.
/// </summary>
public sealed record KnownAgent(
    string AppName,
    string PromptFileName,
    string ProcessName,
    string[] TitleMarkers,
    string TmuxSessionPrefix);

/// <summary>
/// The single source of truth for every CLI agent the detector can recognise.
/// Pure data + simple lookups; no I/O. Adding a new agent is one entry in
/// <see cref="KnownAgents"/>.
/// </summary>
public static class CliAgentRegistry
{
    public static IReadOnlyList<KnownAgent> KnownAgents { get; } = new KnownAgent[]
    {
        new("Claude Code", "ClaudeCodeCorrection", "claude",
            new[] { "Claude Code" }, "claude-"),
        new("OpenCode", "OpenCodeCorrection", "opencode",
            new[] { "OpenCode", "OC |" }, "opencode-"),
        new("Gemini CLI", "GeminiCorrection", "gemini",
            new[] { "Gemini" }, "gemini-"),
        new("Codex", "CodexCorrection", "codex",
            new[] { "Codex" }, "codex-"),
    };

    /// <summary>
    /// Agents that Quick Dictation should auto-target: the focus router will
    /// switch focus to a window of this agent if the user pressed dictate
    /// from somewhere else, and the civility trimmer will strip "Děkuji" /
    /// "Prosím" before paste. Whitelist-based so adding a new agent to
    /// <see cref="KnownAgents"/> doesn't silently change paste behaviour for
    /// chat-style apps where civility is legitimate content.
    /// </summary>
    private static readonly HashSet<string> AutoPasteAgentNames =
        new(StringComparer.OrdinalIgnoreCase) { "Claude Code", "Codex" };

    /// <summary>
    /// True if Quick Dictation should treat <paramref name="appName"/> as an
    /// auto-paste target (focus-route + civility-trim eligible).
    /// </summary>
    public static bool IsAutoPasteAgent(string? appName) =>
        !string.IsNullOrEmpty(appName) && AutoPasteAgentNames.Contains(appName);

    /// <summary>
    /// Returns the agent whose tmux session prefix matches the start of
    /// <paramref name="sessionName"/>, or null if no agent claims the prefix.
    /// Case-insensitive so a user who types "Claude-foo" still wins.
    /// </summary>
    public static KnownAgent? FindByTmuxSession(string sessionName)
    {
        if (string.IsNullOrEmpty(sessionName)) return null;
        foreach (var agent in KnownAgents)
        {
            if (sessionName.StartsWith(agent.TmuxSessionPrefix, StringComparison.OrdinalIgnoreCase))
                return agent;
        }
        return null;
    }

    /// <summary>
    /// Returns the first agent whose <see cref="KnownAgent.TitleMarkers"/>
    /// appears anywhere in <paramref name="title"/>, or null on no match.
    /// </summary>
    public static KnownAgent? FindByTitle(string? title)
    {
        if (string.IsNullOrEmpty(title)) return null;
        foreach (var agent in KnownAgents)
        {
            foreach (var marker in agent.TitleMarkers)
            {
                if (title.Contains(marker, StringComparison.OrdinalIgnoreCase))
                    return agent;
            }
        }
        return null;
    }
}
