using Olbrasoft.Data.Cqrs;

namespace Olbrasoft.VirtualAssistant.Data.Commands.TranscriptionCorrectionCommands;

/// <summary>
/// Command to track that a correction was applied during transcription processing.
/// Used for analytics to measure correction effectiveness.
/// </summary>
public class TrackCorrectionUsageCommand : BaseCommand<bool>
{
    public TrackCorrectionUsageCommand(ICommandExecutor executor) : base(executor) { }
    public TrackCorrectionUsageCommand(IMediator mediator) : base(mediator) { }

    /// <summary>
    /// The ID of the applied correction. Must be greater than 0.
    /// </summary>
    public int CorrectionId { get; set; }

    /// <summary>
    /// Optional context about where the correction was used (e.g., "dictation", "continuous-listening").
    /// </summary>
    public string? Context { get; set; }
}
