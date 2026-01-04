using Olbrasoft.VirtualAssistant.Data.Commands.TranscriptionCorrectionCommands;
using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data.EntityFrameworkCore.CommandHandlers.TranscriptionCorrectionCommandHandlers;

/// <summary>
/// Handler for DeleteTranscriptionCorrectionCommand.
/// Deletes a correction from the database.
/// </summary>
public class DeleteTranscriptionCorrectionCommandHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbCommandHandler<DeleteTranscriptionCorrectionCommand, TranscriptionCorrection>(context)
{
    protected override async Task<bool> GetResultToHandleAsync(DeleteTranscriptionCorrectionCommand command, CancellationToken token)
    {
        if (command.Id <= 0)
            throw new ArgumentException("Id must be greater than 0", nameof(command.Id));

        var correction = await Context.TranscriptionCorrections
            .FirstOrDefaultAsync(c => c.Id == command.Id, token);

        if (correction == null)
            return false;

        Context.TranscriptionCorrections.Remove(correction);
        await Context.SaveChangesAsync(token);
        return true;
    }
}
