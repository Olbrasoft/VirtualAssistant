using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Pipeline.Stages;

/// <summary>
/// Detects repeat text intent and sets flag in context.
/// Pipeline will skip LLM routing and execute repeat action directly.
/// </summary>
public class RepeatTextIntentStage : IVoicePipelineStage
{
    private readonly ILogger<RepeatTextIntentStage> _logger;
    private readonly IRepeatTextIntentService _repeatTextIntent;

    public string StageName => "RepeatTextIntent";

    public RepeatTextIntentStage(
        ILogger<RepeatTextIntentStage> logger,
        IRepeatTextIntentService repeatTextIntent)
    {
        _logger = logger;
        _repeatTextIntent = repeatTextIntent;
    }

    public async Task ProcessAsync(VoicePipelineContext context, CancellationToken cancellationToken)
    {
        var text = context.FilteredText ?? context.Transcription;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _logger.LogDebug("[{StageName}] Checking for repeat text intent...", StageName);
        var repeatIntent = await _repeatTextIntent.DetectIntentAsync(text, cancellationToken);

        if (repeatIntent.IsRepeatTextIntent && repeatIntent.Confidence >= 0.7f)
        {
            _logger.LogInformation("[{StageName}] Repeat text intent detected (confidence: {Confidence:F2})",
                StageName, repeatIntent.Confidence);
            context.IsRepeatTextIntent = true;
            // Don't stop pipeline - let it continue to ActionExecutionStage
            // LlmRoutingStage will skip LLM call when this flag is set
        }
    }
}
