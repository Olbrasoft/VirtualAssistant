using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Enums;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.TextInput;
using VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Handles common action processing logic shared between workers.
/// Prevents code duplication and ensures consistent action handling.
/// </summary>
public class ActionHandlerService : IActionHandlerService
{
    private readonly ILogger<ActionHandlerService> _logger;
    private readonly ITextInputService _textInput;
    private readonly IExternalServiceClient _externalService;
    private readonly IRepeatTextIntentService _repeatTextIntent;
    private readonly IVirtualAssistantSpeaker _speaker;

    public ActionHandlerService(
        ILogger<ActionHandlerService> logger,
        ITextInputService textInput,
        IExternalServiceClient externalService,
        IRepeatTextIntentService repeatTextIntent,
        IVirtualAssistantSpeaker speaker)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _textInput = textInput ?? throw new ArgumentNullException(nameof(textInput));
        _externalService = externalService ?? throw new ArgumentNullException(nameof(externalService));
        _repeatTextIntent = repeatTextIntent ?? throw new ArgumentNullException(nameof(repeatTextIntent));
        _speaker = speaker ?? throw new ArgumentNullException(nameof(speaker));
    }

    /// <summary>
    /// Handles OpenCode action by sending command to OpenCode with appropriate agent.
    /// </summary>
    public async Task HandleOpenCodeAsync(string command, PromptType? promptType, CancellationToken cancellationToken)
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

    /// <summary>
    /// Handles repeat text action by fetching last transcribed text from PTT history.
    /// </summary>
    public async Task HandleRepeatTextAsync(CancellationToken cancellationToken)
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

    /// <summary>
    /// Handles dispatch task action by sending task to target agent.
    /// </summary>
    public async Task HandleDispatchTaskAsync(string targetAgent, CancellationToken cancellationToken)
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
                "AgentNotFound" => "Agent nebyl nalezen.",
                "NoPendingTask" => "Zadny cekajici ukol.",
                "TaskAlreadyAssigned" => "Ukol uz byl prirazen.",
                "RateLimitExceeded" => "Prekrocen limit pozadavku.",
                _ => "Chyba pri odesilani ukolu."
            };
            await _speaker.SpeakAsync(ttsMessage, agentName: null, ct: cancellationToken);
        }
        else
        {
            _logger.LogError("Dispatch failed: {Error}", error);
            await _speaker.SpeakAsync("Nepodarilo se odeslat ukol.", agentName: null, ct: cancellationToken);
        }
    }
}
