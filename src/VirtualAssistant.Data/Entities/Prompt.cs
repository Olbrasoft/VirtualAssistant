namespace Olbrasoft.VirtualAssistant.Data.Entities;

/// <summary>
/// Represents an LLM correction prompt with application context mapping.
/// </summary>
public class Prompt
{
    /// <summary>
    /// Unique identifier for this prompt.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Prompt display name (e.g., "OpenCode Correction", "ClaudeCode Correction").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Application name this prompt is for (e.g., "OpenCode", "Claude Code", "Ferdium", "Default").
    /// </summary>
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>
    /// Pattern to match against DesktopContext.ActiveApplication (e.g., "code", "ferdium", "*" for default).
    /// </summary>
    public string AppIdPattern { get; set; } = string.Empty;

    /// <summary>
    /// Filename of the prompt in Prompts/ directory (e.g., "OpenCodeCorrection.md").
    /// </summary>
    public string PromptFileName { get; set; } = string.Empty;

    /// <summary>
    /// When this prompt was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to LLM corrections that used this prompt.
    /// </summary>
    public ICollection<LlmCorrection> LlmCorrections { get; set; } = new List<LlmCorrection>();
}
