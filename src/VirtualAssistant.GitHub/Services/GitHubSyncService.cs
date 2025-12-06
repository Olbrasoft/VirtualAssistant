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
    private readonly ILogger<GitHubSyncService> _logger;

    public GitHubSyncService(
        VirtualAssistantDbContext dbContext,
        IOptions<GitHubSettings> settings,
        ILogger<GitHubSyncService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;

        // Configure GitHub client with authentication
        _gitHubClient = new GitHubClient(new ProductHeaderValue("VirtualAssistant"));

        var token = settings.Value.Token;
        if (!string.IsNullOrWhiteSpace(token))
        {
            _gitHubClient.Credentials = new Credentials(token);
            _logger.LogInformation("GitHub client configured with authentication token");
        }
        else
        {
            _logger.LogWarning("GitHub client running without authentication - rate limited to 60 requests/hour");
        }
    }

    /// <inheritdoc />
    public async Task<int> SyncRepositoriesAsync(string owner, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting repository sync for owner: {Owner}", owner);

        var repos = await _gitHubClient.Repository.GetAllForUser(owner);
        _logger.LogInformation("Found {Count} repositories for {Owner}", repos.Count, owner);

        var syncedCount = 0;
        foreach (var repo in repos)
        {
            ct.ThrowIfCancellationRequested();
            await UpsertRepositoryAsync(repo, ct);
            syncedCount++;
        }

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Synced {Count} repositories for {Owner}", syncedCount, owner);

        return syncedCount;
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
        _logger.LogInformation("Syncing repository: {Owner}/{Repo}", owner, repoName);

        try
        {
            var repo = await _gitHubClient.Repository.Get(owner, repoName);
            var dbRepo = await UpsertRepositoryAsync(repo, ct);
            await _dbContext.SaveChangesAsync(ct);

            var issuesSynced = await SyncIssuesForRepositoryAsync(dbRepo, ct);
            return (true, issuesSynced);
        }
        catch (NotFoundException)
        {
            _logger.LogWarning("Repository {Owner}/{Repo} not found on GitHub", owner, repoName);
            return (false, 0);
        }
    }

    /// <inheritdoc />
    public async Task<(int ReposSynced, int IssuesSynced)> SyncAllAsync(
        string owner, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting full sync for owner: {Owner}", owner);

        var reposSynced = await SyncRepositoriesAsync(owner, ct);

        var totalIssuesSynced = 0;
        var repositories = await _dbContext.GitHubRepositories
            .Where(r => r.Owner == owner)
            .ToListAsync(ct);

        foreach (var repo in repositories)
        {
            ct.ThrowIfCancellationRequested();
            var issuesSynced = await SyncIssuesForRepositoryAsync(repo, ct);
            totalIssuesSynced += issuesSynced;
        }

        _logger.LogInformation(
            "Full sync completed for {Owner}: {Repos} repos, {Issues} issues",
            owner, reposSynced, totalIssuesSynced);

        return (reposSynced, totalIssuesSynced);
    }

    private async Task<GitHubRepository> UpsertRepositoryAsync(Repository repo, CancellationToken ct)
    {
        var existingRepo = await _dbContext.GitHubRepositories
            .FirstOrDefaultAsync(r => r.FullName == repo.FullName, ct);

        if (existingRepo == null)
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

        var syncedCount = 0;
        foreach (var issue in issues)
        {
            ct.ThrowIfCancellationRequested();

            // Skip pull requests (they appear as issues in the API)
            if (issue.PullRequest != null)
            {
                continue;
            }

            await UpsertIssueAsync(repository.Id, issue, ct);
            syncedCount++;
        }

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Synced {Count} issues for {Repo}", syncedCount, repository.FullName);

        return syncedCount;
    }

    private async Task UpsertIssueAsync(int repositoryId, Issue issue, CancellationToken ct)
    {
        var existingIssue = await _dbContext.GitHubIssues
            .Include(i => i.Agents)
            .FirstOrDefaultAsync(
                i => i.RepositoryId == repositoryId && i.IssueNumber == issue.Number, ct);

        if (existingIssue == null)
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

        // Sync agent labels
        await SyncIssueAgentsAsync(existingIssue, issue.Labels, ct);
    }

    private async Task SyncIssueAgentsAsync(
        GitHubIssue dbIssue, IReadOnlyList<Label> labels, CancellationToken ct)
    {
        // Extract agent labels (format: "agent:xxx")
        var agentLabels = labels
            .Where(l => l.Name.StartsWith("agent:", StringComparison.OrdinalIgnoreCase))
            .Select(l => l.Name.Substring("agent:".Length).ToLowerInvariant())
            .ToHashSet();

        // Get current agents from database
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
}
