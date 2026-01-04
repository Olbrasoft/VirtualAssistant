using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Pipeline.Stages;

/// <summary>
/// Routes text through LLM to determine action (OpenCode, Respond, Ignore, etc.).
/// Sets context.RouterAction and context.RouterResult.
/// </summary>
public class LlmRoutingStage : IVoicePipelineStage
{
    private readonly ILogger<LlmRoutingStage> _logger;
    private readonly ILlmRouterService _llmRouter;

    public string StageName => "LlmRouting";

    public LlmRoutingStage(
        ILogger<LlmRoutingStage> logger,
        ILlmRouterService llmRouter)
    {
        _logger = logger;
        _llmRouter = llmRouter;
    }

    public async Task ProcessAsync(VoicePipelineContext context, CancellationToken cancellationToken)
    {
        // Skip LLM routing if repeat text intent was already detected
        if (context.IsRepeatTextIntent)
        {
            _logger.LogDebug("[{StageName}] Skipping LLM routing - repeat text intent detected", StageName);
            return;
        }

        var text = context.FilteredText ?? context.Transcription;
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogDebug("[{StageName}] No text to route - skipping", StageName);
            return;
        }

        _logger.LogInformation("[{StageName}] Routing to LLM: \"{Text}\"", StageName, text);
        var routerResult = await _llmRouter.RouteAsync(text, false, cancellationToken);

        _logger.LogInformation("[{StageName}] {Provider}: {Action} [{PromptType}] (confidence: {Confidence:F2}, {Time}ms)",
            StageName, _llmRouter.ProviderName, routerResult.Action, routerResult.PromptType,
            routerResult.Confidence, routerResult.ResponseTimeMs);

        if (!string.IsNullOrEmpty(routerResult.Reason))
        {
            _logger.LogDebug("[{StageName}] Reason: {Reason}", StageName, routerResult.Reason);
        }

        context.RouterAction = routerResult.Action;
        context.RouterResult = routerResult;
        context.PromptType = routerResult.PromptType;
        context.TargetAgent = routerResult.TargetAgent;
        context.Response = routerResult.Response;
    }
}
