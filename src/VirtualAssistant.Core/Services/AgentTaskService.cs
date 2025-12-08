using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VirtualAssistant.Data.Dtos;
using VirtualAssistant.Data.Entities;
using VirtualAssistant.Data.EntityFrameworkCore;
using VirtualAssistant.Data.Enums;

namespace VirtualAssistant.Core.Services;

/// <summary>
/// Implementation of inter-agent task queue service.
/// </summary>
public partial class AgentTaskService : IAgentTaskService
{
    private readonly VirtualAssistantDbContext _dbContext;
    private readonly ILogger<AgentTaskService> _logger;

    public AgentTaskService(
        VirtualAssistantDbContext dbContext,
        ILogger<AgentTaskService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<AgentTaskDto> CreateTaskAsync(string sourceAgent, CreateTaskRequest request, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceAgent);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.GithubIssueUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Summary);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetAgent);

        var sourceAgentEntity = await GetAgentByNameAsync(sourceAgent, ct)
            ?? throw new InvalidOperationException($"Agent '{sourceAgent}' not found");

        var targetAgentEntity = await GetAgentByNameAsync(request.TargetAgent, ct)
            ?? throw new InvalidOperationException($"Agent '{request.TargetAgent}' not found");

        // Extract issue number from URL
        var issueNumber = ExtractIssueNumber(request.GithubIssueUrl);

        var entity = new AgentTask
        {
            GithubIssueUrl = request.GithubIssueUrl,
            GithubIssueNumber = issueNumber,
            Summary = request.Summary,
            CreatedByAgentId = sourceAgentEntity.Id,
            TargetAgentId = targetAgentEntity.Id,
            RequiresApproval = request.RequiresApproval,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.AgentTasks.Add(entity);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Task created: {Id} by {Source} for {Target}, issue #{Issue}",
            entity.Id, sourceAgent, request.TargetAgent, issueNumber);

        return MapToDto(entity, sourceAgent, request.TargetAgent);
    }

    public async Task<IReadOnlyList<AgentTaskDto>> GetPendingTasksAsync(string agentName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        var agent = await GetAgentByNameAsync(agentName, ct);
        if (agent == null)
        {
            return [];
        }

        var tasks = await _dbContext.AgentTasks
            .Include(t => t.CreatedByAgent)
            .Include(t => t.TargetAgent)
            .Where(t => t.TargetAgentId == agent.Id)
            .Where(t => t.Status == "sent")
            .OrderBy(t => t.SentAt)
            .ToListAsync(ct);

        return tasks.Select(t => MapToDto(t, t.CreatedByAgent?.Name, t.TargetAgent?.Name)).ToList();
    }

    public async Task<IReadOnlyList<AgentTaskDto>> GetAwaitingApprovalAsync(CancellationToken ct = default)
    {
        var tasks = await _dbContext.AgentTasks
            .Include(t => t.CreatedByAgent)
            .Include(t => t.TargetAgent)
            .Where(t => t.RequiresApproval && t.Status == "pending")
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);

        return tasks.Select(t => MapToDto(t, t.CreatedByAgent?.Name, t.TargetAgent?.Name)).ToList();
    }

    public async Task ApproveTaskAsync(int taskId, CancellationToken ct = default)
    {
        var task = await GetTaskEntityAsync(taskId, ct)
            ?? throw new KeyNotFoundException($"Task {taskId} not found");

        if (task.Status != "pending")
        {
            throw new InvalidOperationException($"Cannot approve task with status '{task.Status}'");
        }

        task.Status = "approved";
        task.ApprovedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Task {Id} approved", taskId);
    }

    public async Task CancelTaskAsync(int taskId, CancellationToken ct = default)
    {
        var task = await GetTaskEntityAsync(taskId, ct)
            ?? throw new KeyNotFoundException($"Task {taskId} not found");

        if (task.Status is not ("pending" or "approved" or "notified"))
        {
            throw new InvalidOperationException($"Cannot cancel task with status '{task.Status}'");
        }

        task.Status = "cancelled";
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Task {Id} cancelled", taskId);
    }

    public async Task CompleteTaskAsync(int taskId, string result, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(result);

        var task = await GetTaskEntityAsync(taskId, ct)
            ?? throw new KeyNotFoundException($"Task {taskId} not found");

        if (task.Status != "sent")
        {
            throw new InvalidOperationException($"Cannot complete task with status '{task.Status}'");
        }

        task.Status = "completed";
        task.Result = result;
        task.CompletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Task {Id} completed with result: {Result}", taskId, result);
    }

    public async Task<AgentTaskDto?> GetTaskAsync(int taskId, CancellationToken ct = default)
    {
        var task = await _dbContext.AgentTasks
            .Include(t => t.CreatedByAgent)
            .Include(t => t.TargetAgent)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct);

        return task != null
            ? MapToDto(task, task.CreatedByAgent?.Name, task.TargetAgent?.Name)
            : null;
    }

    public async Task<IReadOnlyList<AgentTaskDto>> GetAllTasksAsync(int limit = 100, CancellationToken ct = default)
    {
        var tasks = await _dbContext.AgentTasks
            .Include(t => t.CreatedByAgent)
            .Include(t => t.TargetAgent)
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        return tasks.Select(t => MapToDto(t, t.CreatedByAgent?.Name, t.TargetAgent?.Name)).ToList();
    }

    public async Task<bool> IsAgentIdleAsync(string agentName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        // An agent is idle if:
        // 1. They have no AgentResponse records (never started), OR
        // 2. Their last AgentResponse has status = Completed

        var lastResponse = await _dbContext.AgentResponses
            .Where(ar => ar.AgentName == agentName)
            .OrderByDescending(ar => ar.StartedAt)
            .FirstOrDefaultAsync(ct);

        if (lastResponse == null)
        {
            // No responses ever - agent is considered idle
            _logger.LogDebug("Agent {Agent} has no responses, considered idle", agentName);
            return true;
        }

        var isIdle = lastResponse.Status == AgentResponseStatus.Completed;

        _logger.LogDebug(
            "Agent {Agent} last response: {ResponseId}, status: {Status}, idle: {IsIdle}",
            agentName, lastResponse.Id, lastResponse.Status, isIdle);

        return isIdle;
    }

    public async Task<IReadOnlyList<AgentTaskDto>> GetReadyToSendAsync(CancellationToken ct = default)
    {
        // Get tasks that are approved (or don't require approval and are pending)
        var candidates = await _dbContext.AgentTasks
            .Include(t => t.CreatedByAgent)
            .Include(t => t.TargetAgent)
            .Where(t => t.Status == "approved" ||
                       (t.Status == "pending" && !t.RequiresApproval))
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);

        var readyTasks = new List<AgentTaskDto>();

        foreach (var task in candidates)
        {
            if (task.TargetAgent == null)
            {
                continue;
            }

            // Check if target agent is idle
            var isIdle = await IsAgentIdleAsync(task.TargetAgent.Name, ct);
            if (isIdle)
            {
                readyTasks.Add(MapToDto(task, task.CreatedByAgent?.Name, task.TargetAgent.Name));
            }
        }

        _logger.LogDebug("Found {Count} tasks ready to send", readyTasks.Count);

        return readyTasks;
    }

    public async Task MarkSentAsync(int taskId, string deliveryMethod, string? response = null, CancellationToken ct = default)
    {
        var task = await GetTaskEntityAsync(taskId, ct)
            ?? throw new KeyNotFoundException($"Task {taskId} not found");

        if (task.TargetAgentId == null)
        {
            throw new InvalidOperationException("Task has no target agent");
        }

        task.Status = "sent";
        task.SentAt = DateTime.UtcNow;

        // Log the delivery
        var sendLog = new AgentTaskSend
        {
            TaskId = taskId,
            AgentId = task.TargetAgentId.Value,
            SentAt = DateTime.UtcNow,
            DeliveryMethod = deliveryMethod,
            Response = response
        };

        _dbContext.AgentTaskSends.Add(sendLog);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Task {Id} marked as sent via {Method}",
            taskId, deliveryMethod);
    }

    public async Task MarkNotifiedAsync(int taskId, CancellationToken ct = default)
    {
        var task = await GetTaskEntityAsync(taskId, ct)
            ?? throw new KeyNotFoundException($"Task {taskId} not found");

        if (task.Status is not ("pending" or "approved"))
        {
            throw new InvalidOperationException($"Cannot notify task with status '{task.Status}'");
        }

        task.Status = "notified";
        task.NotifiedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Task {Id} marked as notified", taskId);
    }

    public async Task<IReadOnlyList<AgentTaskDto>> GetNotifiedTasksAsync(string agentName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        var agent = await GetAgentByNameAsync(agentName, ct);
        if (agent == null)
        {
            return [];
        }

        var tasks = await _dbContext.AgentTasks
            .Include(t => t.CreatedByAgent)
            .Include(t => t.TargetAgent)
            .Where(t => t.TargetAgentId == agent.Id)
            .Where(t => t.Status == "notified")
            .OrderBy(t => t.NotifiedAt)
            .ToListAsync(ct);

        return tasks.Select(t => MapToDto(t, t.CreatedByAgent?.Name, t.TargetAgent?.Name)).ToList();
    }

    public async Task<string> AcceptTaskAsync(int taskId, CancellationToken ct = default)
    {
        var task = await _dbContext.AgentTasks
            .Include(t => t.CreatedByAgent)
            .Include(t => t.TargetAgent)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new KeyNotFoundException($"Task {taskId} not found");

        if (task.Status != "notified")
        {
            throw new InvalidOperationException($"Cannot accept task with status '{task.Status}'. Task must be in 'notified' status.");
        }

        // Build the prompt
        var prompt = BuildTaskPrompt(task);

        // Mark task as sent (via pull-based delivery)
        task.Status = "sent";
        task.SentAt = DateTime.UtcNow;

        // Log the delivery
        if (task.TargetAgentId.HasValue)
        {
            var sendLog = new AgentTaskSend
            {
                TaskId = taskId,
                AgentId = task.TargetAgentId.Value,
                SentAt = DateTime.UtcNow,
                DeliveryMethod = "pull_api",
                Response = "Task accepted via API"
            };
            _dbContext.AgentTaskSends.Add(sendLog);
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Task {Id} accepted by {Agent} via pull API", taskId, task.TargetAgent?.Name);

        return prompt;
    }

    private static string BuildTaskPrompt(AgentTask task)
    {
        return task.TargetAgent?.Name.ToLowerInvariant() switch
        {
            "claude" => $"""
                Nový úkol k implementaci:
                {task.Summary}

                Issue: {task.GithubIssueUrl}

                Přečti si issue pro detaily, implementuj, otestuj, nasaď.
                """,

            "opencode" => $"""
                Claude dokončil implementaci:
                {task.Summary}

                Issue: {task.GithubIssueUrl}

                Otestuj funkčnost s uživatelem. Pokud je potřeba restart služby, požádej uživatele.
                """,

            _ => $"""
                Nový úkol:
                {task.Summary}

                Issue: {task.GithubIssueUrl}
                """
        };
    }

    private async Task<Agent?> GetAgentByNameAsync(string name, CancellationToken ct)
    {
        return await _dbContext.Agents
            .FirstOrDefaultAsync(a => a.Name == name && a.IsActive, ct);
    }

    private async Task<AgentTask?> GetTaskEntityAsync(int taskId, CancellationToken ct)
    {
        return await _dbContext.AgentTasks.FindAsync([taskId], ct);
    }

    private static int? ExtractIssueNumber(string url)
    {
        var match = IssueNumberRegex().Match(url);
        return match.Success && int.TryParse(match.Groups[1].Value, out var number)
            ? number
            : null;
    }

    private static AgentTaskDto MapToDto(AgentTask entity, string? createdByAgent, string? targetAgent)
    {
        return new AgentTaskDto
        {
            Id = entity.Id,
            GithubIssueUrl = entity.GithubIssueUrl,
            GithubIssueNumber = entity.GithubIssueNumber,
            Summary = entity.Summary,
            CreatedByAgent = createdByAgent,
            TargetAgent = targetAgent,
            Status = entity.Status,
            RequiresApproval = entity.RequiresApproval,
            Result = entity.Result,
            CreatedAt = entity.CreatedAt,
            ApprovedAt = entity.ApprovedAt,
            NotifiedAt = entity.NotifiedAt,
            SentAt = entity.SentAt,
            StartedAt = entity.StartedAt,
            CompletedAt = entity.CompletedAt,
            ClaudeSessionId = entity.ClaudeSessionId
        };
    }

    public async Task<DispatchTaskResult> DispatchTaskAsync(
        string targetAgent,
        int? githubIssueNumber = null,
        string? githubIssueUrl = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetAgent);

        // 1. Check if target agent is available
        var isIdle = await IsAgentIdleAsync(targetAgent, ct);
        if (!isIdle)
        {
            _logger.LogInformation("Dispatch rejected: {Agent} is busy", targetAgent);
            return DispatchTaskResult.AgentBusy(targetAgent);
        }

        // 2. Get the target agent entity
        var agent = await GetAgentByNameAsync(targetAgent, ct);
        if (agent == null)
        {
            _logger.LogWarning("Dispatch failed: Agent '{Agent}' not found", targetAgent);
            return DispatchTaskResult.NoPendingTasks(targetAgent);
        }

        // 3. Find pending task
        AgentTask? task = null;

        // Extract issue number from URL if provided
        if (githubIssueNumber == null && !string.IsNullOrWhiteSpace(githubIssueUrl))
        {
            githubIssueNumber = ExtractIssueNumber(githubIssueUrl);
        }

        if (githubIssueNumber.HasValue)
        {
            // Find specific task by issue number
            task = await _dbContext.AgentTasks
                .Include(t => t.CreatedByAgent)
                .Include(t => t.TargetAgent)
                .Where(t => t.TargetAgentId == agent.Id)
                .Where(t => t.GithubIssueNumber == githubIssueNumber.Value)
                .Where(t => t.Status == "pending" || t.Status == "approved")
                .FirstOrDefaultAsync(ct);

            if (task == null)
            {
                _logger.LogInformation("Dispatch failed: No pending task for issue #{Issue}", githubIssueNumber);
                return DispatchTaskResult.TaskNotFound(githubIssueNumber.Value);
            }
        }
        else
        {
            // Find first pending task for agent
            task = await _dbContext.AgentTasks
                .Include(t => t.CreatedByAgent)
                .Include(t => t.TargetAgent)
                .Where(t => t.TargetAgentId == agent.Id)
                .Where(t => t.Status == "pending" || t.Status == "approved")
                .OrderBy(t => t.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (task == null)
            {
                _logger.LogInformation("Dispatch failed: No pending tasks for {Agent}", targetAgent);
                return DispatchTaskResult.NoPendingTasks(targetAgent);
            }
        }

        // 4. Mark task as sent
        task.Status = "sent";
        task.SentAt = DateTime.UtcNow;

        // Log the delivery
        var sendLog = new AgentTaskSend
        {
            TaskId = task.Id,
            AgentId = agent.Id,
            SentAt = DateTime.UtcNow,
            DeliveryMethod = "dispatch_api",
            Response = "Task dispatched via /api/hub/dispatch-task"
        };

        _dbContext.AgentTaskSends.Add(sendLog);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Task {TaskId} dispatched to {Agent}, issue #{Issue}",
            task.Id, targetAgent, task.GithubIssueNumber);

        return DispatchTaskResult.Dispatched(task.Id, task.GithubIssueNumber, task.GithubIssueUrl, task.Summary);
    }

    [GeneratedRegex(@"/issues/(\d+)")]
    private static partial Regex IssueNumberRegex();
}
