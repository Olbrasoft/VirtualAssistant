using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Enums;
using Olbrasoft.VirtualAssistant.Core.Events;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.TextInput;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Workers;

/// <summary>
/// Worker responsible for executing actions requested by LLM routing.
/// Subscribes to action events and dispatches to appropriate handlers.
/// Single Responsibility: Action execution.
/// </summary>
public class ActionExecutorWorker : BackgroundService
{
    private readonly ILogger<ActionExecutorWorker> _logger;
    private readonly IEventBus _eventBus;
    private readonly IActionHandlerService _actionHandler;
    private readonly IDisposable _actionRequestedSubscription;

    public ActionExecutorWorker(
        ILogger<ActionExecutorWorker> logger,
        IEventBus eventBus,
        IActionHandlerService actionHandler)
    {
        _logger = logger;
        _eventBus = eventBus;
        _actionHandler = actionHandler;

        // Subscribe to action events
        _actionRequestedSubscription = _eventBus.Subscribe<ActionRequestedEvent>(OnActionRequested);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ActionExecutorWorker started");

        try
        {
            // Keep service alive while listening to events
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ActionExecutorWorker stopped");
        }
    }

    private async Task OnActionRequested(ActionRequestedEvent @event, CancellationToken cancellationToken)
    {
        try
        {
            switch (@event.Action)
            {
                case LlmRouterAction.OpenCode:
                case LlmRouterAction.Bash:
                    await _actionHandler.HandleOpenCodeActionAsync(@event.OriginalText, @event.PromptType, cancellationToken);
                    break;

                case LlmRouterAction.Respond:
                    _actionHandler.HandleRespondAction(@event.Response);
                    break;

                case LlmRouterAction.SaveNote:
                    _logger.LogInformation("Note saving not implemented");
                    break;

                case LlmRouterAction.StartDiscussion:
                case LlmRouterAction.EndDiscussion:
                    _logger.LogInformation("Discussion mode not implemented");
                    break;

                case LlmRouterAction.DispatchTask:
                    if (@event.TargetAgent == "repeat-text")
                    {
                        await _actionHandler.HandleRepeatTextAsync(cancellationToken);
                    }
                    else
                    {
                        await _actionHandler.HandleDispatchTaskActionAsync(@event.TargetAgent ?? "claude", cancellationToken);
                    }
                    break;

                case LlmRouterAction.Ignore:
                    // Already logged
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing action {Action}", @event.Action);
        }
    }

    public override void Dispose()
    {
        _actionRequestedSubscription?.Dispose();
        base.Dispose();
    }
}
