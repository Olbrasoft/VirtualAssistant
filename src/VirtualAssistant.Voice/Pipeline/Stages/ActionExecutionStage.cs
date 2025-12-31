using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Enums;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Pipeline.Stages;

/// <summary>
/// Executes actions based on router decision or special intents (repeat text).
/// Final stage in the pipeline.
/// </summary>
public class ActionExecutionStage : IVoicePipelineStage
{
    private readonly ILogger<ActionExecutionStage> _logger;
    private readonly IActionHandlerService _actionHandler;

    public string StageName => "ActionExecution";

    public ActionExecutionStage(
        ILogger<ActionExecutionStage> logger,
        IActionHandlerService actionHandler)
    {
        _logger = logger;
        _actionHandler = actionHandler;
    }

    public async Task ProcessAsync(VoicePipelineContext context, CancellationToken cancellationToken)
    {
        // Handle repeat text intent
        if (context.IsRepeatTextIntent)
        {
            _logger.LogInformation("[{StageName}] Executing repeat text action", StageName);
            await _actionHandler.HandleRepeatTextAsync(cancellationToken);
            return;
        }

        // Handle router actions
        if (context.RouterAction == null)
        {
            _logger.LogDebug("[{StageName}] No router action - skipping", StageName);
            return;
        }

        var text = context.FilteredText ?? context.Transcription ?? string.Empty;

        switch (context.RouterAction.Value)
        {
            case LlmRouterAction.OpenCode:
                await _actionHandler.HandleOpenCodeActionAsync(
                    text,
                    context.PromptType,
                    cancellationToken);
                break;

            case LlmRouterAction.Respond:
                _actionHandler.HandleRespondAction(context.Response ?? string.Empty);
                break;

            case LlmRouterAction.SaveNote:
                _logger.LogInformation("[{StageName}] Note saving not implemented", StageName);
                break;

            case LlmRouterAction.StartDiscussion:
            case LlmRouterAction.EndDiscussion:
                _logger.LogInformation("[{StageName}] Discussion mode not implemented", StageName);
                break;

            case LlmRouterAction.DispatchTask:
                await _actionHandler.HandleDispatchTaskActionAsync(
                    context.TargetAgent ?? "claude",
                    cancellationToken);
                break;

            case LlmRouterAction.Ignore:
                // Already logged by LlmRoutingStage
                break;
        }
    }
}
