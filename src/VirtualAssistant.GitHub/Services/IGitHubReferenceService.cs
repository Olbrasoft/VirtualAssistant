namespace VirtualAssistant.GitHub.Services;

/// <summary>
/// Service for ensuring GitHub repository and issue references exist in the database.
/// </summary>
public interface IGitHubReferenceService
{
    /// <summary>
    /// Ensures a repository exists in the database, creating it if necessary.
    /// </summary>
    /// <param name="owner">Repository owner</param>
    /// <param name="name">Repository name</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The repository ID</returns>
    Task<int> EnsureRepositoryExistsAsync(string owner, string name, CancellationToken ct = default);

    /// <summary>
    /// Ensures an issue exists in the database, creating repository and issue if necessary.
    /// </summary>
    /// <param name="owner">Repository owner</param>
    /// <param name="name">Repository name</param>
    /// <param name="issueNumber">Issue number</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The issue ID</returns>
    Task<int> EnsureIssueExistsAsync(string owner, string name, int issueNumber, CancellationToken ct = default);

    /// <summary>
    /// Parses a GitHub issue URL and ensures the reference exists.
    /// </summary>
    /// <param name="url">GitHub issue URL (e.g., https://github.com/owner/repo/issues/123)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Parsed info and database ID, or null if URL is invalid</returns>
    Task<GitHubIssueReference?> EnsureIssueFromUrlAsync(string url, CancellationToken ct = default);
}

/// <summary>
/// Parsed GitHub issue reference with database ID.
/// </summary>
public record GitHubIssueReference(
    int IssueId,
    int RepositoryId,
    string Owner,
    string Name,
    int IssueNumber)
{
    /// <summary>
    /// Gets the GitHub URL for this issue.
    /// </summary>
    public string Url => $"https://github.com/{Owner}/{Name}/issues/{IssueNumber}";
}
