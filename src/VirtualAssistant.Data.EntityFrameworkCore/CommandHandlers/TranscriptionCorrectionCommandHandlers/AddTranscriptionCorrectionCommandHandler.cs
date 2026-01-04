using Olbrasoft.VirtualAssistant.Data.Commands.TranscriptionCorrectionCommands;
using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data.EntityFrameworkCore.CommandHandlers.TranscriptionCorrectionCommandHandlers;

/// <summary>
/// Handler for AddTranscriptionCorrectionCommand.
/// Adds a new correction to the database.
/// </summary>
public class AddTranscriptionCorrectionCommandHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbCommandHandler<AddTranscriptionCorrectionCommand, TranscriptionCorrection>(context)
{
    protected override async Task<bool> GetResultToHandleAsync(AddTranscriptionCorrectionCommand command, CancellationToken token)
    {
        Context.TranscriptionCorrections.Add(command.Correction);
        await Context.SaveChangesAsync(token);
        return true;
    }
}
