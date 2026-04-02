namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Resolves the context-aware system prompt for LLM correction based on current desktop context.
/// Thread-safe: creates its own DI scope for database queries.
/// </summary>
public interface IPromptResolver
{
    /// <summary>
    /// Resolves the system prompt based on current desktop context (CLI app, window title, default).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of (prompt text, prompt database ID).</returns>
    Task<(string PromptText, int PromptId)> ResolvePromptAsync(CancellationToken ct);
}
