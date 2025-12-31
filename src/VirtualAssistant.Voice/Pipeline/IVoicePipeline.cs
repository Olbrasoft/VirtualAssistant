namespace Olbrasoft.VirtualAssistant.Voice.Pipeline;

/// <summary>
/// Facade interface for the voice processing pipeline.
/// Orchestrates all pipeline stages and manages context flow.
/// </summary>
public interface IVoicePipeline
{
    /// <summary>
    /// Processes audio data through the complete pipeline.
    /// </summary>
    /// <param name="audioData">Raw audio data from microphone.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pipeline context with results.</returns>
    Task<VoicePipelineContext> ProcessAsync(byte[] audioData, CancellationToken cancellationToken);
}
