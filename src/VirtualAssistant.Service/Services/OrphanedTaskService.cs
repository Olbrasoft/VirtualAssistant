using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VirtualAssistant.Core.Services;
using VirtualAssistant.Data.EntityFrameworkCore;
using VirtualAssistant.Data.Enums;
using VirtualAssistant.GitHub.Services;

namespace Olbrasoft.VirtualAssistant.Service.Services;

/// <summary>
/// Service for detecting and managing orphaned tasks after service restart.
/// </summary>
public class OrphanedTaskService : IOrphanedTaskService
{
    private readonly VirtualAssistantDbContext _dbContext;
    private readonly IGitHubIssueStatusService _githubService;
    private readonly ILogger<OrphanedTaskService> _logger;

    // Default GitHub repository for VirtualAssistant
    private const string DefaultOwner = "Olbrasoft";
    private const string DefaultRepo = "VirtualAssistant";

    public OrphanedTaskService(
        VirtualAssistantDbContext dbContext,
        IGitHubIssueStatusService githubService,
        ILogger<OrphanedTaskService> logger)
    {
        _dbContext = dbContext;
        _githubService = githubService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OrphanedTaskInfo>> FindOrphanedTasksAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Searching for orphaned tasks (status=InProgress, completedAt=null)");

        var stuckResponses = await _dbContext.AgentResponses
            .Include(r => r.AgentTask)
            .Where(r => r.Status == AgentResponseStatus.InProgress && r.CompletedAt == null)
            .ToListAsync(ct);

        if (stuckResponses.Count == 0)
        {
            _logger.LogInformation("No orphaned tasks found");
            return [];
        }

        _logger.LogWarning("Found {Count} orphaned task(s)", stuckResponses.Count);

        var results = new List<OrphanedTaskInfo>();

        foreach (var response in stuckResponses)
        {
            var info = new OrphanedTaskInfo
            {
                AgentResponseId = response.Id,
                AgentName = response.AgentName,
                StartedAt = response.StartedAt,
                TaskId = response.AgentTaskId,
                GithubIssueNumber = response.AgentTask?.GithubIssueNumber,
                TaskSummary = response.AgentTask?.Summary,
                AgentStatus = "idle" // We assume idle since service restarted
            };

            // Check GitHub issue status if we have an issue number
            if (response.AgentTask?.GithubIssueNumber.HasValue == true)
            {
                var issueNumber = response.AgentTask.GithubIssueNumber.Value;
                var githubStatus = await _githubService.GetIssueStatusAsync(
                    DefaultOwner, DefaultRepo, issueNumber, ct);

                info = info with { GithubIssueStatus = githubStatus.State };
            }

            results.Add(info);
        }

        return results;
    }

    /// <inheritdoc />
    public async Task MarkAsCompletedAsync(int agentResponseId, CancellationToken ct = default)
    {
        var response = await _dbContext.AgentResponses
            .Include(r => r.AgentTask)
            .FirstOrDefaultAsync(r => r.Id == agentResponseId, ct);

        if (response == null)
        {
            _logger.LogWarning("AgentResponse {Id} not found", agentResponseId);
            return;
        }

        _logger.LogInformation("Marking orphaned task {Id} as completed (human decision)", agentResponseId);

        response.Status = AgentResponseStatus.Completed;
        response.CompletedAt = DateTime.UtcNow;

        // Also complete the related task if exists
        if (response.AgentTask != null)
        {
            response.AgentTask.Status = "completed";
            response.AgentTask.CompletedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Orphaned task {Id} marked as completed", agentResponseId);
    }

    /// <inheritdoc />
    public async Task ResetTaskAsync(int agentResponseId, CancellationToken ct = default)
    {
        var response = await _dbContext.AgentResponses
            .Include(r => r.AgentTask)
            .FirstOrDefaultAsync(r => r.Id == agentResponseId, ct);

        if (response == null)
        {
            _logger.LogWarning("AgentResponse {Id} not found", agentResponseId);
            return;
        }

        _logger.LogInformation("Resetting orphaned task {Id} to pending (human decision)", agentResponseId);

        // Complete the stuck response
        response.Status = AgentResponseStatus.Completed;
        response.CompletedAt = DateTime.UtcNow;

        // Reset the task to pending for retry
        if (response.AgentTask != null)
        {
            response.AgentTask.Status = "pending";
            response.AgentTask.SentAt = null;
            response.AgentTask.CompletedAt = null;
            response.AgentTask.Result = null;
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Orphaned task {Id} reset to pending", agentResponseId);
    }

    /// <inheritdoc />
    public async Task IgnoreAsync(int agentResponseId, CancellationToken ct = default)
    {
        var response = await _dbContext.AgentResponses
            .FirstOrDefaultAsync(r => r.Id == agentResponseId, ct);

        if (response == null)
        {
            _logger.LogWarning("AgentResponse {Id} not found", agentResponseId);
            return;
        }

        _logger.LogInformation("Ignoring orphaned task {Id} (human decision)", agentResponseId);

        // Just complete the response without touching the task
        response.Status = AgentResponseStatus.Completed;
        response.CompletedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Orphaned task {Id} ignored", agentResponseId);
    }
}
