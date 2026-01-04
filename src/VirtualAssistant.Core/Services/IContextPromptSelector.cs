using Olbrasoft.VirtualAssistant.Core.Models;

namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Service for selecting appropriate system prompt based on desktop context.
/// </summary>
public interface IContextPromptSelector
{
    /// <summary>
    /// Selects appropriate system prompt based on desktop context.
    /// Returns prompt file path relative to prompts directory.
    /// </summary>
    Task<string> SelectPromptAsync(DesktopContext? context, CancellationToken ct = default);

    /// <summary>
    /// Detects context type from application ID.
    /// </summary>
    ContextType DetectContextType(string applicationId);
}

/// <summary>
/// Type of desktop context for prompt selection.
/// </summary>
public enum ContextType
{
    Programming,
    Chat,
    Browsing,
    General
}
