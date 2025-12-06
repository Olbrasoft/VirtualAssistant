namespace VirtualAssistant.GitHub.Dtos;

/// <summary>
/// Data transfer object for GitHub issue search results.
/// </summary>
public class GitHubIssueDto
{
    /// <summary>
    /// Gets or sets the database ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the issue number within the repository.
    /// </summary>
    public int IssueNumber { get; set; }

    /// <summary>
    /// Gets or sets the issue title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the issue body (truncated to 500 characters).
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
    /// Gets or sets the full repository name (e.g., "Olbrasoft/VirtualAssistant").
    /// </summary>
    public string RepositoryFullName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the similarity score (0.0 to 1.0) for search results.
    /// Null for non-search queries.
    /// </summary>
    public float? Similarity { get; set; }

    /// <summary>
    /// Gets or sets the list of agent names assigned to this issue.
    /// </summary>
    public List<string> Agents { get; set; } = new();

    /// <summary>
    /// Gets or sets when the issue was created on GitHub.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the issue was last updated on GitHub.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Maximum length for truncated body.
    /// </summary>
    private const int MaxBodyLength = 500;

    /// <summary>
    /// Creates a DTO from a GitHubIssue entity.
    /// </summary>
    public static GitHubIssueDto FromEntity(
        VirtualAssistant.Data.Entities.GitHubIssue issue,
        float? similarity = null)
    {
        return new GitHubIssueDto
        {
            Id = issue.Id,
            IssueNumber = issue.IssueNumber,
            Title = issue.Title,
            Body = TruncateBody(issue.Body),
            State = issue.State,
            HtmlUrl = issue.HtmlUrl,
            RepositoryFullName = issue.Repository?.FullName ?? string.Empty,
            Similarity = similarity,
            Agents = issue.Agents?.Select(a => a.Agent).ToList() ?? new List<string>(),
            CreatedAt = issue.CreatedAt,
            UpdatedAt = issue.UpdatedAt
        };
    }

    private static string? TruncateBody(string? body)
    {
        if (string.IsNullOrEmpty(body))
            return body;

        if (body.Length <= MaxBodyLength)
            return body;

        return body[..MaxBodyLength] + "...";
    }
}
