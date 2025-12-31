namespace Olbrasoft.VirtualAssistant.Voice.Pipeline;

/// <summary>
/// Represents a single stage in the voice processing pipeline.
/// Each stage processes audio data, transcription, or performs actions.
/// </summary>
public interface IVoicePipelineStage
{
    /// <summary>
    /// Gets the name of the stage for logging purposes.
    /// </summary>
    string StageName { get; }

    /// <summary>
    /// Processes the pipeline context and updates it with results.
    /// </summary>
    /// <param name="context">The pipeline context containing audio data, transcription, and state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    Task ProcessAsync(VoicePipelineContext context, CancellationToken cancellationToken);
}
