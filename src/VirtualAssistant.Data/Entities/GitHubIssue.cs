using Olbrasoft.Data.Entities.Abstractions;

namespace VirtualAssistant.Data.Entities;

/// <summary>
/// Minimal reference to a GitHub issue.
/// URL can be constructed from repository + issue number.
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
    /// Gets or sets the navigation property to the repository.
    /// </summary>
    public GitHubRepository Repository { get; set; } = null!;

    /// <summary>
    /// Constructs the GitHub URL for this issue.
    /// </summary>
    public string GetUrl() => $"{Repository.GetUrl()}/issues/{IssueNumber}";
}
