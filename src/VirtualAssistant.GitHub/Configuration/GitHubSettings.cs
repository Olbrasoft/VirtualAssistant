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
    /// Gets or sets the default GitHub owner (user or organization) to sync.
    /// </summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether scheduled automatic sync is enabled.
    /// Default: true
    /// </summary>
    public bool EnableScheduledSync { get; set; } = true;

    /// <summary>
    /// Gets or sets the interval between automatic syncs in minutes.
    /// Default: 60 (every hour)
    /// </summary>
    public int SyncIntervalMinutes { get; set; } = 60;
}
