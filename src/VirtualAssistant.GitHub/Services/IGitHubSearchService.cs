using VirtualAssistant.GitHub.Dtos;

namespace VirtualAssistant.GitHub.Services;

/// <summary>
/// Interface for semantic search on GitHub issues using vector similarity.
/// </summary>
public interface IGitHubSearchService
{
    /// <summary>
    /// Searches for similar issues using semantic vector similarity.
    /// </summary>
    /// <param name="query">The search query text.</param>
    /// <param name="target">Which fields to search (title, body, or both).</param>
    /// <param name="limit">Maximum number of results to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of matching issues ordered by similarity.</returns>
    Task<IReadOnlyList<GitHubIssueDto>> SearchSimilarAsync(
        string query,
        SearchTarget target = SearchTarget.Both,
        int limit = 10,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all open issues for a repository.
    /// </summary>
    /// <param name="repoFullName">Full repository name (e.g., "Olbrasoft/VirtualAssistant").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of open issues.</returns>
    Task<IReadOnlyList<GitHubIssueDto>> GetOpenIssuesAsync(
        string repoFullName,
        CancellationToken ct = default);

    /// <summary>
    /// Finds potential duplicate issues by semantic similarity.
    /// </summary>
    /// <param name="title">The title of the potential new issue.</param>
    /// <param name="body">The body of the potential new issue (optional).</param>
    /// <param name="threshold">Minimum similarity score (0.0 to 1.0) to consider as duplicate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of potentially duplicate issues above the threshold.</returns>
    Task<IReadOnlyList<GitHubIssueDto>> FindDuplicatesAsync(
        string title,
        string? body = null,
        float threshold = 0.8f,
        CancellationToken ct = default);

    /// <summary>
    /// Gets whether the search service is configured and ready.
    /// </summary>
    bool IsConfigured { get; }
}
