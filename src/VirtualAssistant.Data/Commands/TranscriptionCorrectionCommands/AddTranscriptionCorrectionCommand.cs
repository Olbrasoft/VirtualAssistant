using Olbrasoft.Data.Cqrs;
using VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Commands.TranscriptionCorrectionCommands;

/// <summary>
/// Command to add a new correction to the database.
/// </summary>
public class AddTranscriptionCorrectionCommand : BaseCommand<bool>
{
    public AddTranscriptionCorrectionCommand(ICommandExecutor executor) : base(executor) { }
    public AddTranscriptionCorrectionCommand(IMediator mediator) : base(mediator) { }

    /// <summary>
    /// The correction to add. Must not be null.
    /// </summary>
    public TranscriptionCorrection Correction { get; set; } = null!;
}
