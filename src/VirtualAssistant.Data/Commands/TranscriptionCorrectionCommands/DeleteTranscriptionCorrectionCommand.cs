using Olbrasoft.Data.Cqrs;

namespace Olbrasoft.VirtualAssistant.Data.Commands.TranscriptionCorrectionCommands;

/// <summary>
/// Command to delete a correction from the database.
/// </summary>
public class DeleteTranscriptionCorrectionCommand : BaseCommand<bool>
{
    public DeleteTranscriptionCorrectionCommand(ICommandExecutor executor) : base(executor) { }
    public DeleteTranscriptionCorrectionCommand(IMediator mediator) : base(mediator) { }

    /// <summary>
    /// The ID of the correction to delete. Must be greater than 0.
    /// </summary>
    public int Id { get; set; } = -1;
}
