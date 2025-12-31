using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Pipeline.Stages;

/// <summary>
/// Detects stop commands and stops pipeline if detected.
/// </summary>
public class StopCommandStage : IVoicePipelineStage
{
    private readonly ILogger<StopCommandStage> _logger;
    private readonly ICommandDetectionService _commandDetection;

    public string StageName => "StopCommand";

    public StopCommandStage(
        ILogger<StopCommandStage> logger,
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

        if (_commandDetection.IsStopCommand(text))
        {
            _logger.LogInformation("[{StageName}] Stop command detected - stopping pipeline", StageName);
            context.ShouldStop = true;
            context.StopReason = "Stop command";
        }

        return Task.CompletedTask;
    }
}
