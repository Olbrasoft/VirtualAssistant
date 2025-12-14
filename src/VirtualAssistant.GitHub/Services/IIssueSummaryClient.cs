namespace VirtualAssistant.GitHub.Services;

/// <summary>
/// Client for fetching translated issue summaries from the GitHub.Issues API.
/// </summary>
public interface IIssueSummaryClient
{
    /// <summary>
    /// Gets translated summaries for the specified issues.
    /// If an issue is not in the database, the API will fetch it from GitHub.
    /// </summary>
    /// <param name="owner">Repository owner</param>
    /// <param name="repo">Repository name</param>
    /// <param name="issueNumbers">List of issue numbers</param>
    /// <param name="languageId">Language LCID (default: 1029 = Czech)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Summaries result with found issues and any that weren't found</returns>
    Task<IssueSummariesResult> GetSummariesAsync(
        string owner,
        string repo,
        IEnumerable<int> issueNumbers,
        int languageId = 1029,
        CancellationToken ct = default);
}

/// <summary>
/// Result from fetching issue summaries.
/// </summary>
public class IssueSummariesResult
{
    /// <summary>
    /// Dictionary mapping issue numbers to their summaries.
    /// </summary>
    public Dictionary<int, IssueSummary> Summaries { get; set; } = new();

    /// <summary>
    /// Issue numbers that were synced from GitHub during this request.
    /// </summary>
    public List<int> SyncedFromGitHub { get; set; } = new();

    /// <summary>
    /// Issue numbers that could not be found (neither in DB nor on GitHub).
    /// </summary>
    public List<int> NotFound { get; set; } = new();

    /// <summary>
    /// Error message if the request failed, null otherwise.
    /// </summary>
    public string? Error { get; set; }
}

/// <summary>
/// Summary information for a single issue.
/// </summary>
public class IssueSummary
{
    /// <summary>
    /// GitHub issue number.
    /// </summary>
    public int IssueNumber { get; set; }

    /// <summary>
    /// Original English title.
    /// </summary>
    public string OriginalTitle { get; set; } = string.Empty;

    /// <summary>
    /// Translated Czech title.
    /// </summary>
    public string CzechTitle { get; set; } = string.Empty;

    /// <summary>
    /// AI-generated Czech summary of the issue.
    /// </summary>
    public string CzechSummary { get; set; } = string.Empty;

    /// <summary>
    /// Whether the issue is currently open.
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// GitHub URL of the issue.
    /// </summary>
    public string Url { get; set; } = string.Empty;
}
