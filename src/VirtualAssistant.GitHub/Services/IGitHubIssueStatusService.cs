namespace VirtualAssistant.GitHub.Services;

/// <summary>
/// Service for checking GitHub issue status.
/// </summary>
public interface IGitHubIssueStatusService
{
    /// <summary>
    /// Get the status of a GitHub issue.
    /// </summary>
    /// <param name="owner">Repository owner</param>
    /// <param name="repo">Repository name</param>
    /// <param name="issueNumber">Issue number</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Issue status information</returns>
    Task<GitHubIssueStatus> GetIssueStatusAsync(string owner, string repo, int issueNumber, CancellationToken ct = default);
}

/// <summary>
/// GitHub issue status information.
/// </summary>
public class GitHubIssueStatus
{
    /// <summary>
    /// Issue number.
    /// </summary>
    public int Number { get; init; }

    /// <summary>
    /// Issue state: "open" or "closed".
    /// </summary>
    public string State { get; init; } = "unknown";

    /// <summary>
    /// Issue title.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Whether the issue was found.
    /// </summary>
    public bool Found { get; init; }

    /// <summary>
    /// Error message if lookup failed.
    /// </summary>
    public string? Error { get; init; }
}
