using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;
using VirtualAssistant.GitHub.Configuration;

namespace VirtualAssistant.GitHub.Services;

/// <summary>
/// Implementation of GitHub issue status service using Octokit.NET.
/// </summary>
public class GitHubIssueStatusService : IGitHubIssueStatusService
{
    private readonly GitHubClient _gitHubClient;
    private readonly ILogger<GitHubIssueStatusService> _logger;

    public GitHubIssueStatusService(
        IOptions<GitHubSettings> settings,
        ILogger<GitHubIssueStatusService> logger)
    {
        _logger = logger;

        _gitHubClient = new GitHubClient(new ProductHeaderValue("VirtualAssistant"));

        var token = settings.Value.Token;
        if (!string.IsNullOrWhiteSpace(token))
        {
            _gitHubClient.Credentials = new Credentials(token);
        }
    }

    /// <inheritdoc />
    public async Task<GitHubIssueStatus> GetIssueStatusAsync(
        string owner,
        string repo,
        int issueNumber,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogDebug("Checking GitHub issue status: {Owner}/{Repo}#{Number}", owner, repo, issueNumber);

            var issue = await _gitHubClient.Issue.Get(owner, repo, issueNumber);

            return new GitHubIssueStatus
            {
                Number = issueNumber,
                State = issue.State.StringValue,
                Title = issue.Title,
                Found = true
            };
        }
        catch (NotFoundException)
        {
            _logger.LogWarning("GitHub issue not found: {Owner}/{Repo}#{Number}", owner, repo, issueNumber);
            return new GitHubIssueStatus
            {
                Number = issueNumber,
                State = "not_found",
                Found = false,
                Error = $"Issue #{issueNumber} not found"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check GitHub issue status: {Owner}/{Repo}#{Number}", owner, repo, issueNumber);
            return new GitHubIssueStatus
            {
                Number = issueNumber,
                State = "error",
                Found = false,
                Error = ex.Message
            };
        }
    }
}
