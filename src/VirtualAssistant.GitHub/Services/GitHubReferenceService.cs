using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VirtualAssistant.Data.Entities;
using VirtualAssistant.Data.EntityFrameworkCore;

namespace VirtualAssistant.GitHub.Services;

/// <summary>
/// Service for ensuring GitHub repository and issue references exist in the database.
/// </summary>
public partial class GitHubReferenceService : IGitHubReferenceService
{
    private readonly VirtualAssistantDbContext _dbContext;
    private readonly ILogger<GitHubReferenceService> _logger;

    /// <summary>
    /// Regex to parse GitHub issue URLs.
    /// Matches: https://github.com/{owner}/{repo}/issues/{number}
    /// </summary>
    [GeneratedRegex(@"^https?://github\.com/(?<owner>[^/]+)/(?<repo>[^/]+)/issues/(?<number>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex GitHubIssueUrlRegex();

    public GitHubReferenceService(
        VirtualAssistantDbContext dbContext,
        ILogger<GitHubReferenceService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> EnsureRepositoryExistsAsync(string owner, string name, CancellationToken ct = default)
    {
        var repo = await _dbContext.GitHubRepositories
            .FirstOrDefaultAsync(r => r.Owner == owner && r.Name == name, ct);

        if (repo != null)
            return repo.Id;

        repo = new GitHubRepository { Owner = owner, Name = name };
        _dbContext.GitHubRepositories.Add(repo);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created GitHub repository reference: {Owner}/{Name} (ID: {Id})", owner, name, repo.Id);
        return repo.Id;
    }

    /// <inheritdoc />
    public async Task<int> EnsureIssueExistsAsync(string owner, string name, int issueNumber, CancellationToken ct = default)
    {
        var repoId = await EnsureRepositoryExistsAsync(owner, name, ct);

        var issue = await _dbContext.GitHubIssues
            .FirstOrDefaultAsync(i => i.RepositoryId == repoId && i.IssueNumber == issueNumber, ct);

        if (issue != null)
            return issue.Id;

        issue = new GitHubIssue { RepositoryId = repoId, IssueNumber = issueNumber };
        _dbContext.GitHubIssues.Add(issue);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created GitHub issue reference: {Owner}/{Name}#{Number} (ID: {Id})",
            owner, name, issueNumber, issue.Id);
        return issue.Id;
    }

    /// <inheritdoc />
    public async Task<GitHubIssueReference?> EnsureIssueFromUrlAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var match = GitHubIssueUrlRegex().Match(url);
        if (!match.Success)
        {
            _logger.LogWarning("Invalid GitHub issue URL format: {Url}", url);
            return null;
        }

        var owner = match.Groups["owner"].Value;
        var name = match.Groups["repo"].Value;

        if (!int.TryParse(match.Groups["number"].Value, out var issueNumber))
        {
            _logger.LogWarning("Invalid issue number in URL: {Url}", url);
            return null;
        }

        var repoId = await EnsureRepositoryExistsAsync(owner, name, ct);
        var issueId = await EnsureIssueExistsAsync(owner, name, issueNumber, ct);

        return new GitHubIssueReference(issueId, repoId, owner, name, issueNumber);
    }
}
