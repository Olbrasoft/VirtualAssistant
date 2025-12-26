using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Enums;
using Olbrasoft.VirtualAssistant.Core.Events;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.TextInput;
using Olbrasoft.VirtualAssistant.Voice.Services;
using VirtualAssistant.Core.Services;

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
    private readonly ITextInputService _textInput;
    private readonly IVirtualAssistantSpeaker _speaker;
    private readonly IExternalServiceClient _externalService;
    private readonly IRepeatTextIntentService _repeatTextIntent;
    private readonly IDisposable _actionRequestedSubscription;

    public ActionExecutorWorker(
        ILogger<ActionExecutorWorker> logger,
        IEventBus eventBus,
        ITextInputService textInput,
        IVirtualAssistantSpeaker speaker,
        IExternalServiceClient externalService,
        IRepeatTextIntentService repeatTextIntent)
    {
        _logger = logger;
        _eventBus = eventBus;
        _textInput = textInput;
        _speaker = speaker;
        _externalService = externalService;
        _repeatTextIntent = repeatTextIntent;

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
                    await HandleOpenCodeActionAsync(@event.OriginalText, @event.PromptType, cancellationToken);
                    break;

                case LlmRouterAction.Respond:
                    HandleRespondAction(@event.Response);
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
                        await HandleRepeatTextAsync(cancellationToken);
                    }
                    else
                    {
                        await HandleDispatchTaskActionAsync(@event.TargetAgent ?? "claude", cancellationToken);
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

    private async Task HandleOpenCodeActionAsync(string command, PromptType? promptType, CancellationToken cancellationToken)
    {
        var agent = promptType switch
        {
            PromptType.Command => "build",
            PromptType.Confirmation => "build",
            PromptType.Continuation => "build",
            PromptType.Question => "plan",
            PromptType.Acknowledgement => "plan",
            _ => "plan"
        };

        _logger.LogInformation("Sending to OpenCode with agent: {Agent}", agent);
        var success = await _textInput.SendMessageToSessionAsync(command, agent, cancellationToken);

        if (success)
        {
            _logger.LogInformation("Message sent to OpenCode");
        }
        else
        {
            _logger.LogWarning("Failed to send message to OpenCode");
        }
    }

    private void HandleRespondAction(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            _logger.LogWarning("LLM returned RESPOND but no response text");
            return;
        }

        _logger.LogInformation("Response: \"{Response}\"", response);
    }

    private async Task HandleRepeatTextAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Calling PTT repeat endpoint");
        var (success, response, error) = await _externalService.CallPttRepeatAsync(cancellationToken);

        if (success && response != null)
        {
            var preview = response.Text?.Length > 50 ? response.Text[..50] + "..." : response.Text;
            _logger.LogInformation("Text copied to clipboard: \"{Text}\"", preview);
            var phrase = _repeatTextIntent.GetRandomClipboardResponse();
            await _speaker.SpeakAsync(phrase, agentName: null, ct: cancellationToken);
        }
        else if (error == "No text in history")
        {
            _logger.LogWarning("No text in history");
            await _speaker.SpeakAsync("Zadny text v historii.", agentName: null, ct: cancellationToken);
        }
        else
        {
            _logger.LogError("PTT repeat failed: {Error}", error);
            await _speaker.SpeakAsync("Nepodarilo se ziskat text.", agentName: null, ct: cancellationToken);
        }
    }

    private async Task HandleDispatchTaskActionAsync(string targetAgent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Dispatching task to {Agent}", targetAgent);
        var (success, response, error) = await _externalService.DispatchTaskAsync(targetAgent, cancellationToken);

        if (success && response?.Success == true)
        {
            var issueInfo = response.GithubIssueNumber.HasValue ? $" (issue #{response.GithubIssueNumber})" : "";
            _logger.LogInformation("Task dispatched to {Agent}{IssueInfo}", targetAgent, issueInfo);

            var ttsMessage = response.GithubIssueNumber.HasValue
                ? $"Posilam ukol cislo {response.GithubIssueNumber}."
                : "Ukol odeslan.";
            await _speaker.SpeakAsync(ttsMessage, agentName: null, ct: cancellationToken);
        }
        else if (response != null)
        {
            _logger.LogWarning("{Message}", response.Message);

            var ttsMessage = response.Reason switch
            {
                "agent_busy" => $"{targetAgent} je zaneprazdneny.",
                "no_pending_tasks" => "Zadne cekajici ukoly.",
                _ => response.Message ?? "Nepodarilo se odeslat ukol."
            };
            await _speaker.SpeakAsync(ttsMessage, agentName: null, ct: cancellationToken);
        }
        else
        {
            _logger.LogError("Dispatch failed: {Error}", error);
            await _speaker.SpeakAsync("Chyba pri odesilani ukolu.", agentName: null, ct: cancellationToken);
        }
    }

    public override void Dispose()
    {
        _actionRequestedSubscription?.Dispose();
        base.Dispose();
    }
}
