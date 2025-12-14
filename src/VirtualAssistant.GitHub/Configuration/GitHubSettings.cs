namespace VirtualAssistant.GitHub.Configuration;

/// <summary>
/// Configuration settings for GitHub integration.
/// </summary>
public class GitHubSettings
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "GitHub";

    /// <summary>
    /// Gets or sets the GitHub Personal Access Token (PAT).
    /// Required for API access and higher rate limits (5000 req/hour vs 60/hour).
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default GitHub owner (user or organization).
    /// </summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the URL of the GitHub.Issues API for fetching Czech summaries.
    /// Example: "http://localhost:5000"
    /// </summary>
    public string IssuesApiUrl { get; set; } = string.Empty;
}
