namespace VirtualAssistant.GitHub.Services;

/// <summary>
/// Service for synchronizing GitHub repositories and issues to the local database.
/// </summary>
public interface IGitHubSyncService
{
    /// <summary>
    /// Synchronizes all repositories for the specified owner.
    /// </summary>
    /// <param name="owner">GitHub owner (user or organization).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of repositories synced.</returns>
    Task<int> SyncRepositoriesAsync(string owner, CancellationToken ct = default);

    /// <summary>
    /// Synchronizes all issues for a specific repository.
    /// </summary>
    /// <param name="repositoryId">Local database repository ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of issues synced.</returns>
    Task<int> SyncIssuesAsync(int repositoryId, CancellationToken ct = default);

    /// <summary>
    /// Synchronizes a single repository and its issues.
    /// </summary>
    /// <param name="owner">GitHub owner (user or organization).</param>
    /// <param name="repoName">Repository name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of (repository synced, issues synced).</returns>
    Task<(bool RepoSynced, int IssuesSynced)> SyncRepositoryAsync(string owner, string repoName, CancellationToken ct = default);

    /// <summary>
    /// Synchronizes all repositories and their issues for the specified owner.
    /// </summary>
    /// <param name="owner">GitHub owner (user or organization).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of (repositories synced, total issues synced).</returns>
    Task<(int ReposSynced, int IssuesSynced)> SyncAllAsync(string owner, CancellationToken ct = default);
}
