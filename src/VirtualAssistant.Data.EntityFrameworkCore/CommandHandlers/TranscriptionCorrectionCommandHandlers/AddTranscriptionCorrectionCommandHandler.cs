using Olbrasoft.VirtualAssistant.Data.Commands.TranscriptionCorrectionCommands;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.CommandHandlers.TranscriptionCorrectionCommandHandlers;

/// <summary>
/// Handler for AddTranscriptionCorrectionCommand.
/// Adds a new correction to the database.
/// </summary>
public class AddTranscriptionCorrectionCommandHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbCommandHandler<AddTranscriptionCorrectionCommand, TranscriptionCorrection>(context)
{
    protected override async Task<bool> GetResultToHandleAsync(AddTranscriptionCorrectionCommand command, CancellationToken token)
    {
        command.Correction.CreatedAt = DateTimeOffset.UtcNow;
        command.Correction.UpdatedAt = DateTimeOffset.UtcNow;

        Context.TranscriptionCorrections.Add(command.Correction);
        await Context.SaveChangesAsync(token);
        return true;
    }
}
