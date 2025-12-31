using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Voice.Pipeline;

/// <summary>
/// Voice processing pipeline that orchestrates all stages.
/// Uses Chain of Responsibility pattern to process audio through multiple stages.
/// </summary>
public class VoicePipeline : IVoicePipeline
{
    private readonly ILogger<VoicePipeline> _logger;
    private readonly IEnumerable<IVoicePipelineStage> _stages;

    public VoicePipeline(
        ILogger<VoicePipeline> logger,
        IEnumerable<IVoicePipelineStage> stages)
    {
        _logger = logger;
        _stages = stages;
    }

    public async Task<VoicePipelineContext> ProcessAsync(byte[] audioData, CancellationToken cancellationToken)
    {
        var context = new VoicePipelineContext
        {
            AudioData = audioData
        };

        _logger.LogDebug("Starting voice pipeline with {StageCount} stages", _stages.Count());

        foreach (var stage in _stages)
        {
            if (context.ShouldStop)
            {
                _logger.LogDebug("Pipeline stopped after stage {StageName}: {StopReason}",
                    stage.StageName, context.StopReason);
                break;
            }

            try
            {
                await stage.ProcessAsync(context, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Pipeline cancelled by user at stage {StageName}", stage.StageName);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in pipeline stage {StageName}", stage.StageName);
                throw;
            }
        }

        _logger.LogDebug("Voice pipeline completed");
        return context;
    }
}
