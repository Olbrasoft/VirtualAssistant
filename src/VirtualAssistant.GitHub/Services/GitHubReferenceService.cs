using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Data.Commands.GitHubCommands;

namespace Olbrasoft.VirtualAssistant.GitHub.Services;

/// <summary>
/// Service for ensuring GitHub repository and issue references exist in the database.
/// Uses CQRS pattern for data access.
/// </summary>
public partial class GitHubReferenceService : IGitHubReferenceService
{
    private readonly ICommandExecutor _commandExecutor;
    private readonly ILogger<GitHubReferenceService> _logger;

    /// <summary>
    /// Regex to parse GitHub issue URLs.
    /// Matches: https://github.com/{owner}/{repo}/issues/{number}
    /// </summary>
    [GeneratedRegex(@"^https?://github\.com/(?<owner>[^/]+)/(?<repo>[^/]+)/issues/(?<number>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex GitHubIssueUrlRegex();

    public GitHubReferenceService(
        ICommandExecutor commandExecutor,
        ILogger<GitHubReferenceService> logger)
    {
        _commandExecutor = commandExecutor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> EnsureRepositoryExistsAsync(string owner, string name, CancellationToken ct = default)
    {
        var command = new EnsureRepositoryExistsCommand(owner, name);
        var repoId = await _commandExecutor.ExecuteAsync(command, ct);

        _logger.LogDebug("Ensured GitHub repository reference: {Owner}/{Name} (ID: {Id})", owner, name, repoId);
        return repoId;
    }

    /// <inheritdoc />
    public async Task<int> EnsureIssueExistsAsync(string owner, string name, int issueNumber, CancellationToken ct = default)
    {
        var command = new EnsureIssueExistsCommand(owner, name, issueNumber);
        var issueId = await _commandExecutor.ExecuteAsync(command, ct);

        _logger.LogDebug("Ensured GitHub issue reference: {Owner}/{Name}#{Number} (ID: {Id})",
            owner, name, issueNumber, issueId);
        return issueId;
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
