using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VirtualAssistant.Data.Dtos;

namespace VirtualAssistant.Core.Services;

/// <summary>
/// Background service that distributes tasks to idle agents.
/// Periodically checks for approved tasks and sends them when target agent is idle.
/// </summary>
public class TaskDistributionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TaskDistributionService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(10);

    public TaskDistributionService(
        IServiceScopeFactory scopeFactory,
        ILogger<TaskDistributionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Task Distribution Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DistributeTasksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in task distribution loop");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Task Distribution Service stopped");
    }

    private async Task DistributeTasksAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var taskService = scope.ServiceProvider.GetRequiredService<IAgentTaskService>();
        var hubService = scope.ServiceProvider.GetRequiredService<IAgentHubService>();
        var ttsService = scope.ServiceProvider.GetRequiredService<ITtsNotificationService>();

        var readyTasks = await taskService.GetReadyToSendAsync(ct);

        if (readyTasks.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Found {Count} tasks ready to distribute", readyTasks.Count);

        foreach (var task in readyTasks)
        {
            try
            {
                await SendTaskToAgentAsync(task, taskService, hubService, ttsService, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send task {TaskId} to {Agent}", task.Id, task.TargetAgent);
            }
        }
    }

    private async Task SendTaskToAgentAsync(
        AgentTaskDto task,
        IAgentTaskService taskService,
        IAgentHubService hubService,
        ITtsNotificationService ttsService,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(task.TargetAgent))
        {
            _logger.LogWarning("Task {TaskId} has no target agent", task.Id);
            return;
        }

        var targetAgentLower = task.TargetAgent.ToLowerInvariant();

        // OpenCode uses pull-based delivery: only notify, don't auto-send
        if (targetAgentLower == "opencode")
        {
            await NotifyAgentAsync(task, taskService, ttsService, ct);
            return;
        }

        // Claude and other agents use push-based delivery: auto-send via hub
        await PushTaskToAgentAsync(task, taskService, hubService, ttsService, ct);
    }

    /// <summary>
    /// Notify agent about pending task (pull-based delivery for OpenCode).
    /// Agent must call AcceptTask to receive the task prompt.
    /// </summary>
    private async Task NotifyAgentAsync(
        AgentTaskDto task,
        IAgentTaskService taskService,
        ITtsNotificationService ttsService,
        CancellationToken ct)
    {
        // Mark task as notified
        await taskService.MarkNotifiedAsync(task.Id, ct);

        // Notify user via TTS
        var notification = $"Máš nový úkol od Clauda: issue {task.GithubIssueNumber}.";
        await ttsService.SpeakAsync(notification, source: "assistant", ct);

        _logger.LogInformation(
            "Task {TaskId} notified to {Agent} (pull-based delivery)",
            task.Id, task.TargetAgent);
    }

    /// <summary>
    /// Push task directly to agent via hub (for Claude and other agents).
    /// </summary>
    private async Task PushTaskToAgentAsync(
        AgentTaskDto task,
        IAgentTaskService taskService,
        IAgentHubService hubService,
        ITtsNotificationService ttsService,
        CancellationToken ct)
    {
        // Build the prompt based on target agent
        var prompt = BuildTaskPrompt(task);

        // Send via hub as a new task start
        var sessionId = $"task-{task.Id}";
        var messageId = await hubService.StartTaskAsync(
            sourceAgent: "virtualassistant",
            content: prompt,
            targetAgent: task.TargetAgent!,
            sessionId: sessionId,
            ct: ct);

        // Mark task as sent
        await taskService.MarkSentAsync(
            task.Id,
            deliveryMethod: "hub_api",
            response: $"Message ID: {messageId}",
            ct);

        // Notify user via TTS
        var notification = $"Posílám úkol Claudovi: issue {task.GithubIssueNumber}.";
        await ttsService.SpeakAsync(notification, source: "assistant", ct);

        _logger.LogInformation(
            "Task {TaskId} sent to {Agent}, hub message {MessageId}",
            task.Id, task.TargetAgent, messageId);
    }

    private static string BuildTaskPrompt(AgentTaskDto task)
    {
        return task.TargetAgent?.ToLowerInvariant() switch
        {
            "claude" => $"""
                Nový úkol k implementaci:
                {task.Summary}

                Issue: {task.GithubIssueUrl}

                Přečti si issue pro detaily, implementuj, otestuj, nasaď.
                """,

            _ => $"""
                Nový úkol:
                {task.Summary}

                Issue: {task.GithubIssueUrl}
                """
        };
    }
}
