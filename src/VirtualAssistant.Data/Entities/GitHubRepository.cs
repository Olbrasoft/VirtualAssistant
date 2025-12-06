using Olbrasoft.Data.Entities.Abstractions;

namespace VirtualAssistant.Data.Entities;

/// <summary>
/// Represents a GitHub repository synced to local database.
/// </summary>
public class GitHubRepository : BaseEnity
{
    /// <summary>
    /// Gets or sets the repository owner (username or organization).
    /// </summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the repository name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full repository name (owner/name).
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the repository description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the HTML URL to the repository.
    /// </summary>
    public string HtmlUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the repository is private.
    /// </summary>
    public bool IsPrivate { get; set; }

    /// <summary>
    /// Gets or sets when the repository was created on GitHub.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the repository was last updated on GitHub.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the repository was last synced to local database.
    /// </summary>
    public DateTime SyncedAt { get; set; }

    /// <summary>
    /// Gets or sets the collection of issues in this repository.
    /// </summary>
    public ICollection<GitHubIssue> Issues { get; set; } = [];
}
