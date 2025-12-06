namespace VirtualAssistant.GitHub.Services;

/// <summary>
/// Specifies which fields to search when performing semantic search.
/// </summary>
public enum SearchTarget
{
    /// <summary>
    /// Search only in issue titles.
    /// </summary>
    Title,

    /// <summary>
    /// Search only in issue bodies.
    /// </summary>
    Body,

    /// <summary>
    /// Search in both titles and bodies (default).
    /// </summary>
    Both
}
