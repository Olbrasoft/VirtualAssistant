namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Resolves the system prompt that an LLM provider should use for the current
/// desktop context. Extracted from <see cref="LlmProviderBase"/> so every
/// provider shares the same priority-cascade logic and so the 85-line method
/// no longer lives on the abstract base.
/// </summary>
/// <remarks>
/// Priority cascade:
/// 1. CLI app running in the focused terminal (Claude Code, OpenCode, Gemini) →
///    prompt keyed by <c>CliAppDescriptor.PromptFileName</c>.
/// 2. Window-title / application-ID pattern match in the <c>prompts</c> table.
/// 3. Default prompt (AppIdPattern = "*").
/// 4. Hardcoded "DefaultCorrection" cache lookup + ID 4 fallback (last-resort
///    when the default prompt row is missing).
/// </remarks>
public interface ISystemPromptResolver
{
    /// <summary>
    /// Resolves the system prompt for the currently focused window.
    /// </summary>
    /// <returns>Tuple of (prompt text, prompt ID). Never returns null; falls
    /// back to a hardcoded prompt if every priority level fails.</returns>
    Task<(string PromptText, int PromptId)> ResolveAsync(CancellationToken cancellationToken = default);
}
