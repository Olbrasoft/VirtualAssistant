using Olbrasoft.Data.Entities.Abstractions;
using Pgvector;

namespace VirtualAssistant.Data.Entities;

/// <summary>
/// Represents a GitHub issue synced to local database.
/// </summary>
public class GitHubIssue : BaseEnity
{
    /// <summary>
    /// Gets or sets the foreign key to the repository.
    /// </summary>
    public int RepositoryId { get; set; }

    /// <summary>
    /// Gets or sets the issue number within the repository.
    /// </summary>
    public int IssueNumber { get; set; }

    /// <summary>
    /// Gets or sets the issue title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the issue body/description.
    /// </summary>
    public string? Body { get; set; }

    /// <summary>
    /// Gets or sets the issue state ("open" or "closed").
    /// </summary>
    public string State { get; set; } = "open";

    /// <summary>
    /// Gets or sets the HTML URL to the issue.
    /// </summary>
    public string HtmlUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the issue was created on GitHub.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the issue was last updated on GitHub.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the issue was last synced to local database.
    /// </summary>
    public DateTime SyncedAt { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the repository.
    /// </summary>
    public GitHubRepository Repository { get; set; } = null!;

    /// <summary>
    /// Gets or sets the collection of agent assignments for this issue.
    /// Supports multiple agents: claude, opencode, user.
    /// </summary>
    public ICollection<GitHubIssueAgent> Agents { get; set; } = new List<GitHubIssueAgent>();

    /// <summary>
    /// Gets or sets the vector embedding for the issue title.
    /// Uses text-embedding-3-small model (1536 dimensions).
    /// </summary>
    public Vector? TitleEmbedding { get; set; }

    /// <summary>
    /// Gets or sets the vector embedding for the issue body.
    /// Uses text-embedding-3-small model (1536 dimensions).
    /// </summary>
    public Vector? BodyEmbedding { get; set; }

    /// <summary>
    /// Gets or sets when the embeddings were last generated.
    /// Null if embeddings have never been generated.
    /// </summary>
    public DateTime? EmbeddingGeneratedAt { get; set; }
}
