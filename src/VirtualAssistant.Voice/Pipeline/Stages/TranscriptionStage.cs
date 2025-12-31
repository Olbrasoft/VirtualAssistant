using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Pipeline.Stages;

/// <summary>
/// Transcribes audio data using Whisper.
/// Sets context.Transcription and stops pipeline if transcription fails.
/// </summary>
public class TranscriptionStage : IVoicePipelineStage
{
    private readonly ILogger<TranscriptionStage> _logger;
    private readonly TranscriptionService _transcription;

    public string StageName => "Transcription";

    public TranscriptionStage(
        ILogger<TranscriptionStage> logger,
        TranscriptionService transcription)
    {
        _logger = logger;
        _transcription = transcription;
    }

    public async Task ProcessAsync(VoicePipelineContext context, CancellationToken cancellationToken)
    {
        if (context.AudioData == null || context.AudioData.Length == 0)
        {
            _logger.LogWarning("[{StageName}] No audio data - stopping pipeline", StageName);
            context.ShouldStop = true;
            context.StopReason = "No audio data";
            return;
        }

        _logger.LogDebug("[{StageName}] Transcribing audio ({Size} bytes)...", StageName, context.AudioData.Length);

        var result = await _transcription.TranscribeAsync(context.AudioData, cancellationToken);

        if (!result.Success || string.IsNullOrWhiteSpace(result.Text))
        {
            _logger.LogDebug("[{StageName}] Transcription failed or empty - stopping pipeline", StageName);
            context.ShouldStop = true;
            context.StopReason = "Transcription failed or empty";
            return;
        }

        context.Transcription = result.Text;
        _logger.LogInformation("[{StageName}] Transcribed: \"{Text}\"", StageName, result.Text);
    }
}
