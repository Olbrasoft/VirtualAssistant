using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Pipeline.Stages;

/// <summary>
/// Applies local pre-filtering (too short, noise detection).
/// Stops pipeline if text should be skipped locally.
/// </summary>
public class LocalFilterStage : IVoicePipelineStage
{
    private readonly ILogger<LocalFilterStage> _logger;
    private readonly ICommandDetectionService _commandDetection;

    public string StageName => "LocalFilter";

    public LocalFilterStage(
        ILogger<LocalFilterStage> logger,
        ICommandDetectionService commandDetection)
    {
        _logger = logger;
        _commandDetection = commandDetection;
    }

    public Task ProcessAsync(VoicePipelineContext context, CancellationToken cancellationToken)
    {
        var text = context.FilteredText ?? context.Transcription;
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.CompletedTask;
        }

        if (_commandDetection.ShouldSkipLocally(text))
        {
            _logger.LogDebug("[{StageName}] Text should be skipped locally (too short or noise) - stopping pipeline", StageName);
            context.ShouldStop = true;
            context.StopReason = "Skipped locally (too short or noise)";
        }

        return Task.CompletedTask;
    }
}
