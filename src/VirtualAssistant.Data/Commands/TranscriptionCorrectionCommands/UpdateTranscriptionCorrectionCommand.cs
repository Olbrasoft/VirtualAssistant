using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Commands.TranscriptionCorrectionCommands;

/// <summary>
/// Command to update an existing correction in the database.
/// Sets UpdatedAt to current time automatically.
/// </summary>
public class UpdateTranscriptionCorrectionCommand : BaseCommand<bool>
{
    public UpdateTranscriptionCorrectionCommand(ICommandExecutor executor) : base(executor) { }
    public UpdateTranscriptionCorrectionCommand(IMediator mediator) : base(mediator) { }

    /// <summary>
    /// The correction to update. Must not be null.
    /// </summary>
    public TranscriptionCorrection Correction { get; set; } = null!;
}
