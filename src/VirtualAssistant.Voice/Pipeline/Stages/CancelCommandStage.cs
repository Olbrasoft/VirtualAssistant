using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Pipeline.Stages;

/// <summary>
/// Detects cancel commands and stops pipeline if detected.
/// </summary>
public class CancelCommandStage : IVoicePipelineStage
{
    private readonly ILogger<CancelCommandStage> _logger;
    private readonly ICommandDetectionService _commandDetection;

    public string StageName => "CancelCommand";

    public CancelCommandStage(
        ILogger<CancelCommandStage> logger,
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

        if (_commandDetection.IsCancelCommand(text))
        {
            _logger.LogInformation("[{StageName}] Cancel command detected - stopping pipeline", StageName);
            context.ShouldStop = true;
            context.StopReason = "Cancel command";
        }

        return Task.CompletedTask;
    }
}
