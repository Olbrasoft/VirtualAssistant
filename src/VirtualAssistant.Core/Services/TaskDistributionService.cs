using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VirtualAssistant.Data.Dtos;

namespace VirtualAssistant.Core.Services;

/// <summary>
/// Background service that distributes tasks to agents.
/// Periodically checks for approved tasks and notifies when ready.
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
        var speaker = scope.ServiceProvider.GetRequiredService<IVirtualAssistantSpeaker>();

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
                await NotifyTaskAsync(task, taskService, speaker, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify task {TaskId} to {Agent}", task.Id, task.TargetAgent);
            }
        }
    }

    /// <summary>
    /// Notify about pending task via TTS.
    /// Agent must call AcceptTask to receive the task prompt.
    /// </summary>
    private async Task NotifyTaskAsync(
        AgentTaskDto task,
        IAgentTaskService taskService,
        IVirtualAssistantSpeaker speaker,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(task.TargetAgent))
        {
            _logger.LogWarning("Task {TaskId} has no target agent", task.Id);
            return;
        }

        // Mark task as notified
        await taskService.MarkNotifiedAsync(task.Id, ct);

        // Notify user via TTS
        var notification = $"Nový úkol pro {task.TargetAgent}: issue {task.GithubIssueNumber}.";
        await speaker.SpeakAsync(notification, task.TargetAgent, ct);

        _logger.LogInformation(
            "Task {TaskId} notified to {Agent}",
            task.Id, task.TargetAgent);
    }
}
