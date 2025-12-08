using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Services;

/// <summary>
/// Background service that detects orphaned tasks on startup and notifies the human.
/// Does NOT auto-fix - only informs and waits for human decision.
/// </summary>
public sealed class OrphanedTaskDetectionService : IHostedService
{
    private readonly IOrphanedTaskService _orphanedTaskService;
    private readonly IVirtualAssistantSpeaker _speaker;
    private readonly ILogger<OrphanedTaskDetectionService> _logger;

    public OrphanedTaskDetectionService(
        IOrphanedTaskService orphanedTaskService,
        IVirtualAssistantSpeaker speaker,
        ILogger<OrphanedTaskDetectionService> logger)
    {
        _orphanedTaskService = orphanedTaskService;
        _speaker = speaker;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Wait for other services to initialize
        await Task.Delay(2000, cancellationToken);

        _logger.LogInformation("Checking for orphaned tasks after service restart");

        try
        {
            var orphanedTasks = await _orphanedTaskService.FindOrphanedTasksAsync(cancellationToken);

            if (orphanedTasks.Count == 0)
            {
                _logger.LogInformation("No orphaned tasks detected");
                return;
            }

            // Log all orphaned tasks
            foreach (var task in orphanedTasks)
            {
                _logger.LogWarning(
                    "Orphaned task detected: ResponseId={ResponseId}, Agent={Agent}, TaskId={TaskId}, " +
                    "Issue=#{Issue}, GitHub={GithubStatus}, Started={StartedAt}",
                    task.AgentResponseId,
                    task.AgentName,
                    task.TaskId,
                    task.GithubIssueNumber,
                    task.GithubIssueStatus,
                    task.StartedAt);
            }

            // Notify human via TTS
            var message = BuildNotificationMessage(orphanedTasks);
            await _speaker.SpeakAsync(message, agentName: null, cancellationToken);

            _logger.LogInformation(
                "Human notified about {Count} orphaned task(s). Waiting for human decision via API.",
                orphanedTasks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for orphaned tasks");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static string BuildNotificationMessage(IReadOnlyList<OrphanedTaskInfo> tasks)
    {
        if (tasks.Count == 1)
        {
            var task = tasks[0];
            var issueInfo = task.GithubIssueNumber.HasValue
                ? $"Issue číslo {task.GithubIssueNumber} je {TranslateStatus(task.GithubIssueStatus)}."
                : "";

            return $"Pozor, našel jsem osiřelý úkol. {issueInfo} Agent {task.AgentName} vypadá nečinně. Co s tím mám udělat?";
        }

        return $"Pozor, našel jsem {tasks.Count} osiřelých úkolů. Zkontroluj je prosím přes API.";
    }

    private static string TranslateStatus(string? status) => status switch
    {
        "open" => "otevřený",
        "closed" => "zavřený",
        "not_found" => "nenalezený",
        _ => "v neznámém stavu"
    };
}
