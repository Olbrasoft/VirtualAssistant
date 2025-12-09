using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;
using VirtualAssistant.Data.Entities;
using VirtualAssistant.Data.EntityFrameworkCore;
using VirtualAssistant.GitHub.Configuration;

namespace VirtualAssistant.GitHub.Services;

/// <summary>
/// Implementation of GitHub sync service using Octokit.NET.
/// Fetches repositories and issues from GitHub API and stores them in the local database.
/// </summary>
public class GitHubSyncService : IGitHubSyncService
{
    private readonly VirtualAssistantDbContext _dbContext;
    private readonly GitHubClient _gitHubClient;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<GitHubSyncService> _logger;

    public GitHubSyncService(
        VirtualAssistantDbContext dbContext,
        IOptions<GitHubSettings> settings,
        IEmbeddingService embeddingService,
        ILogger<GitHubSyncService> logger)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _logger = logger;

        // Configure GitHub client with authentication
        _gitHubClient = new GitHubClient(new ProductHeaderValue("VirtualAssistant"));

        var token = settings.Value.Token;
        if (!string.IsNullOrWhiteSpace(token))
        {
            _gitHubClient.Credentials = new Credentials(token);
            // Log only partial token for security (first 4 chars + last 4 chars)
            var maskedToken = token.Length > 8
                ? $"{token[..4]}...{token[^4..]}"
                : "****";
            _logger.LogInformation("GitHub client configured with authentication token: {MaskedToken}", maskedToken);
        }
        else
        {
            _logger.LogWarning("GitHub client running without authentication - rate limited to 60 requests/hour. " +
                "Configure GitHub:Token in appsettings.json for 5000 requests/hour.");
        }
    }

    /// <inheritdoc />
    public async Task<int> SyncRepositoriesAsync(string owner, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);

        _logger.LogInformation("Starting repository sync for owner: {Owner}", owner);

        try
        {
            var repos = await _gitHubClient.Repository.GetAllForUser(owner);
            _logger.LogInformation("Found {Count} repositories for {Owner}", repos.Count, owner);

            // OPTIMIZATION: Batch load all existing repos in ONE query (prevents N+1)
            var repoFullNames = repos.Select(r => r.FullName).ToList();
            var existingRepos = await _dbContext.GitHubRepositories
                .Where(r => repoFullNames.Contains(r.FullName))
                .ToDictionaryAsync(r => r.FullName, ct);
            _logger.LogDebug("Batch loaded {Count} existing repositories", existingRepos.Count);

            var syncedCount = 0;
            foreach (var repo in repos)
            {
                ct.ThrowIfCancellationRequested();
                UpsertRepositoryBatch(repo, existingRepos);
                syncedCount++;
            }

            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("Synced {Count} repositories for {Owner}", syncedCount, owner);

            return syncedCount;
        }
        catch (RateLimitExceededException ex)
        {
            _logger.LogError(ex,
                "GitHub API rate limit exceeded. Reset at {ResetTime}. Limit: {Limit}, Remaining: {Remaining}",
                ex.Reset, ex.Limit, ex.Remaining);
            throw;
        }
        catch (NotFoundException)
        {
            _logger.LogWarning("GitHub user/organization '{Owner}' not found", owner);
            return 0;
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "GitHub API error during repository sync for {Owner}: {Message}",
                owner, ex.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> SyncIssuesAsync(int repositoryId, CancellationToken ct = default)
    {
        var repository = await _dbContext.GitHubRepositories
            .FirstOrDefaultAsync(r => r.Id == repositoryId, ct);

        if (repository == null)
        {
            _logger.LogWarning("Repository with ID {Id} not found", repositoryId);
            return 0;
        }

        return await SyncIssuesForRepositoryAsync(repository, ct);
    }

    /// <inheritdoc />
    public async Task<(bool RepoSynced, int IssuesSynced)> SyncRepositoryAsync(
        string owner, string repoName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repoName);

        _logger.LogInformation("Syncing repository: {Owner}/{Repo}", owner, repoName);

        try
        {
            var repo = await _gitHubClient.Repository.Get(owner, repoName);
            var dbRepo = await UpsertRepositoryAsync(repo, ct);
            await _dbContext.SaveChangesAsync(ct);

            var issuesSynced = await SyncIssuesForRepositoryAsync(dbRepo, ct);
            return (true, issuesSynced);
        }
        catch (RateLimitExceededException ex)
        {
            _logger.LogError(ex,
                "GitHub API rate limit exceeded. Reset at {ResetTime}. Limit: {Limit}, Remaining: {Remaining}",
                ex.Reset, ex.Limit, ex.Remaining);
            throw;
        }
        catch (NotFoundException)
        {
            _logger.LogWarning("Repository {Owner}/{Repo} not found on GitHub", owner, repoName);
            return (false, 0);
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "GitHub API error during sync for {Owner}/{Repo}: {Message}",
                owner, repoName, ex.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<(int ReposSynced, int IssuesSynced)> SyncAllAsync(
        string owner, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);

        _logger.LogInformation("Starting full sync for owner: {Owner}", owner);

        try
        {
            var reposSynced = await SyncRepositoriesAsync(owner, ct);

            var totalIssuesSynced = 0;
            var repositories = await _dbContext.GitHubRepositories
                .Where(r => r.Owner == owner)
                .ToListAsync(ct);

            foreach (var repo in repositories)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var issuesSynced = await SyncIssuesForRepositoryAsync(repo, ct);
                    totalIssuesSynced += issuesSynced;
                }
                catch (RateLimitExceededException)
                {
                    _logger.LogWarning(
                        "Rate limit hit during issue sync for {Repo}. Partial sync completed: {Repos} repos, {Issues} issues",
                        repo.FullName, reposSynced, totalIssuesSynced);
                    throw;
                }
                catch (ApiException ex)
                {
                    _logger.LogError(ex, "Error syncing issues for {Repo}, continuing with next repository",
                        repo.FullName);
                    // Continue with next repository instead of failing entire sync
                }
            }

            _logger.LogInformation(
                "Full sync completed for {Owner}: {Repos} repos, {Issues} issues",
                owner, reposSynced, totalIssuesSynced);

            return (reposSynced, totalIssuesSynced);
        }
        catch (RateLimitExceededException)
        {
            // Already logged in inner methods
            throw;
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "GitHub API error during full sync for {Owner}: {Message}",
                owner, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Upserts a repository using pre-loaded batch dictionary (O(1) lookup).
    /// </summary>
    private GitHubRepository UpsertRepositoryBatch(
        Repository repo,
        Dictionary<string, GitHubRepository> existingRepos)
    {
        if (!existingRepos.TryGetValue(repo.FullName, out var existingRepo))
        {
            existingRepo = new GitHubRepository
            {
                Owner = repo.Owner.Login,
                Name = repo.Name,
                FullName = repo.FullName,
                Description = repo.Description,
                HtmlUrl = repo.HtmlUrl,
                IsPrivate = repo.Private,
                CreatedAt = repo.CreatedAt.UtcDateTime,
                UpdatedAt = repo.UpdatedAt.UtcDateTime,
                SyncedAt = DateTime.UtcNow
            };
            _dbContext.GitHubRepositories.Add(existingRepo);
            existingRepos[repo.FullName] = existingRepo; // Add to cache for duplicates
            _logger.LogDebug("Added new repository: {FullName}", repo.FullName);
        }
        else
        {
            existingRepo.Description = repo.Description;
            existingRepo.HtmlUrl = repo.HtmlUrl;
            existingRepo.IsPrivate = repo.Private;
            existingRepo.UpdatedAt = repo.UpdatedAt.UtcDateTime;
            existingRepo.SyncedAt = DateTime.UtcNow;
            _logger.LogDebug("Updated repository: {FullName}", repo.FullName);
        }

        return existingRepo;
    }

    /// <summary>
    /// Upserts a single repository (used for single-repo sync).
    /// </summary>
    private async Task<GitHubRepository> UpsertRepositoryAsync(Repository repo, CancellationToken ct)
    {
        var existingRepo = await _dbContext.GitHubRepositories
            .FirstOrDefaultAsync(r => r.FullName == repo.FullName, ct);

        var repoDict = existingRepo != null
            ? new Dictionary<string, GitHubRepository> { [repo.FullName] = existingRepo }
            : new Dictionary<string, GitHubRepository>();

        return UpsertRepositoryBatch(repo, repoDict);
    }

    private async Task<int> SyncIssuesForRepositoryAsync(GitHubRepository repository, CancellationToken ct)
    {
        _logger.LogInformation("Syncing issues for repository: {FullName}", repository.FullName);

        // Fetch all issues (open and closed, including pull requests)
        var issueRequest = new RepositoryIssueRequest
        {
            State = ItemStateFilter.All,
            Filter = IssueFilter.All
        };

        var options = new ApiOptions
        {
            PageSize = 100
        };

        var issues = await _gitHubClient.Issue.GetAllForRepository(
            repository.Owner, repository.Name, issueRequest, options);

        _logger.LogInformation("Found {Count} issues in {Repo}", issues.Count, repository.FullName);

        // Filter out pull requests first
        var actualIssues = issues.Where(i => i.PullRequest == null).ToList();

        // OPTIMIZATION: Batch load all existing issues in ONE query (prevents N+1)
        var issueNumbers = actualIssues.Select(i => i.Number).ToList();
        var existingIssues = await _dbContext.GitHubIssues
            .Include(i => i.Agents)
            .Where(i => i.RepositoryId == repository.Id && issueNumbers.Contains(i.IssueNumber))
            .ToDictionaryAsync(i => i.IssueNumber, ct);
        _logger.LogDebug("Batch loaded {Count} existing issues", existingIssues.Count);

        var syncedCount = 0;
        foreach (var issue in actualIssues)
        {
            ct.ThrowIfCancellationRequested();
            UpsertIssueBatch(repository.Id, issue, existingIssues);
            syncedCount++;
        }

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Synced {Count} issues for {Repo}", syncedCount, repository.FullName);

        return syncedCount;
    }

    /// <summary>
    /// Upserts an issue using pre-loaded batch dictionary (O(1) lookup).
    /// </summary>
    private void UpsertIssueBatch(
        int repositoryId,
        Issue issue,
        Dictionary<int, GitHubIssue> existingIssues)
    {
        if (!existingIssues.TryGetValue(issue.Number, out var existingIssue))
        {
            existingIssue = new GitHubIssue
            {
                RepositoryId = repositoryId,
                IssueNumber = issue.Number,
                Title = issue.Title,
                Body = issue.Body,
                State = issue.State.StringValue,
                HtmlUrl = issue.HtmlUrl,
                CreatedAt = issue.CreatedAt.UtcDateTime,
                UpdatedAt = issue.UpdatedAt?.UtcDateTime ?? issue.CreatedAt.UtcDateTime,
                SyncedAt = DateTime.UtcNow
            };
            _dbContext.GitHubIssues.Add(existingIssue);
            existingIssues[issue.Number] = existingIssue; // Add to cache for duplicates
            _logger.LogDebug("Added new issue: #{Number} {Title}", issue.Number, issue.Title);
        }
        else
        {
            existingIssue.Title = issue.Title;
            existingIssue.Body = issue.Body;
            existingIssue.State = issue.State.StringValue;
            existingIssue.HtmlUrl = issue.HtmlUrl;
            existingIssue.UpdatedAt = issue.UpdatedAt?.UtcDateTime ?? issue.CreatedAt.UtcDateTime;
            existingIssue.SyncedAt = DateTime.UtcNow;
            _logger.LogDebug("Updated issue: #{Number} {Title}", issue.Number, issue.Title);
        }

        // Sync agent labels (in-memory operation, no DB query)
        SyncIssueAgentsBatch(existingIssue, issue.Labels);
    }

    /// <summary>
    /// Syncs agent labels for an issue (in-memory, no database queries).
    /// Agents collection is already loaded via Include().
    /// </summary>
    private void SyncIssueAgentsBatch(GitHubIssue dbIssue, IReadOnlyList<Label> labels)
    {
        // Extract agent labels (format: "agent:xxx")
        var agentLabels = labels
            .Where(l => l.Name.StartsWith("agent:", StringComparison.OrdinalIgnoreCase))
            .Select(l => l.Name.Substring("agent:".Length).ToLowerInvariant())
            .ToHashSet();

        // Get current agents from in-memory collection
        var currentAgents = dbIssue.Agents.Select(a => a.Agent).ToHashSet();

        // Remove agents that are no longer labeled
        var toRemove = dbIssue.Agents
            .Where(a => !agentLabels.Contains(a.Agent))
            .ToList();

        foreach (var agent in toRemove)
        {
            _dbContext.GitHubIssueAgents.Remove(agent);
            _logger.LogDebug("Removed agent {Agent} from issue #{Number}",
                agent.Agent, dbIssue.IssueNumber);
        }

        // Add new agents
        foreach (var agentName in agentLabels)
        {
            if (!currentAgents.Contains(agentName))
            {
                dbIssue.Agents.Add(new GitHubIssueAgent
                {
                    GitHubIssueId = dbIssue.Id,
                    Agent = agentName
                });
                _logger.LogDebug("Added agent {Agent} to issue #{Number}",
                    agentName, dbIssue.IssueNumber);
            }
        }
    }

    /// <inheritdoc />
    public async Task<int> GenerateMissingEmbeddingsAsync(CancellationToken ct = default)
    {
        if (!_embeddingService.IsConfigured)
        {
            _logger.LogWarning("Embedding service not configured, skipping embedding generation");
            return 0;
        }

        // Find issues without embeddings
        var issuesWithoutEmbeddings = await _dbContext.GitHubIssues
            .Where(i => i.EmbeddingGeneratedAt == null)
            .ToListAsync(ct);

        if (issuesWithoutEmbeddings.Count == 0)
        {
            _logger.LogInformation("All issues already have embeddings");
            return 0;
        }

        _logger.LogInformation("Generating embeddings for {Count} issues", issuesWithoutEmbeddings.Count);

        var generatedCount = 0;
        foreach (var issue in issuesWithoutEmbeddings)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // Generate title embedding
                var titleEmbedding = await _embeddingService.GenerateEmbeddingAsync(issue.Title, ct);
                issue.TitleEmbedding = titleEmbedding;

                // Generate body embedding
                var bodyEmbedding = await _embeddingService.GenerateEmbeddingAsync(issue.Body, ct);
                issue.BodyEmbedding = bodyEmbedding;

                issue.EmbeddingGeneratedAt = DateTime.UtcNow;
                generatedCount++;

                _logger.LogDebug("Generated embeddings for issue #{Number}: Title={HasTitle}, Body={HasBody}",
                    issue.IssueNumber,
                    titleEmbedding != null,
                    bodyEmbedding != null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embedding for issue #{Number}", issue.IssueNumber);
                // Continue with next issue
            }
        }

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Generated embeddings for {Count} issues", generatedCount);

        return generatedCount;
    }
}
