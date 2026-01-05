using Olbrasoft.VirtualAssistant.Data.Commands.TranscriptionCorrectionCommands;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.CommandHandlers.TranscriptionCorrectionCommandHandlers;

/// <summary>
/// Handler for UpdateTranscriptionCorrectionCommand.
/// Updates an existing correction in the database.
/// </summary>
public class UpdateTranscriptionCorrectionCommandHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbCommandHandler<UpdateTranscriptionCorrectionCommand, TranscriptionCorrection>(context)
{
    protected override async Task<bool> GetResultToHandleAsync(UpdateTranscriptionCorrectionCommand command, CancellationToken token)
    {
        command.Correction.UpdatedAt = DateTimeOffset.UtcNow;
        Context.TranscriptionCorrections.Update(command.Correction);
        await Context.SaveChangesAsync(token);
        return true;
    }
}
