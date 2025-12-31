using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Pipeline.Stages;

/// <summary>
/// Filters out TTS echo from transcription using AssistantSpeechTrackerService.
/// Sets context.FilteredText and stops pipeline if entire transcription is echo.
/// </summary>
public class EchoFilterStage : IVoicePipelineStage
{
    private readonly ILogger<EchoFilterStage> _logger;
    private readonly AssistantSpeechTrackerService _speechTracker;

    public string StageName => "EchoFilter";

    public EchoFilterStage(
        ILogger<EchoFilterStage> logger,
        AssistantSpeechTrackerService speechTracker)
    {
        _logger = logger;
        _speechTracker = speechTracker;
    }

    public Task ProcessAsync(VoicePipelineContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.Transcription))
        {
            _logger.LogDebug("[{StageName}] No transcription to filter - skipping", StageName);
            return Task.CompletedTask;
        }

        var filteredText = _speechTracker.FilterEchoFromTranscription(context.Transcription);

        if (string.IsNullOrWhiteSpace(filteredText))
        {
            _logger.LogDebug("[{StageName}] Entire transcription was TTS echo - stopping pipeline", StageName);
            context.ShouldStop = true;
            context.StopReason = "Echo detected (entire transcription was TTS output)";
            return Task.CompletedTask;
        }

        if (filteredText != context.Transcription)
        {
            _logger.LogDebug("[{StageName}] Echo filtered: \"{FilteredText}\"", StageName, filteredText);
        }

        context.FilteredText = filteredText;
        return Task.CompletedTask;
    }
}
