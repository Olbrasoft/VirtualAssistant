using Microsoft.EntityFrameworkCore;
using Olbrasoft.VirtualAssistant.Data.Commands.TranscriptionCorrectionCommands;
using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data.EntityFrameworkCore.CommandHandlers.TranscriptionCorrectionCommandHandlers;

/// <summary>
/// Handler for TrackCorrectionUsageCommand.
/// Tracks that a correction was applied during transcription processing.
/// </summary>
public class TrackCorrectionUsageCommandHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbCommandHandler<TrackCorrectionUsageCommand, TranscriptionCorrection>(context)
{
    protected override async Task<bool> GetResultToHandleAsync(TrackCorrectionUsageCommand command, CancellationToken token)
    {
        // Ensure the referenced correction exists
        var exists = await Context.TranscriptionCorrections
            .AnyAsync(c => c.Id == command.CorrectionId, token);

        if (!exists)
        {
            return false;
        }

        // Record usage of the correction for analytics
        var usage = new TranscriptionCorrectionUsage
        {
            // Link the usage record to the applied correction
            CorrectionId = command.CorrectionId
        };

        Context.Add(usage);
        await Context.SaveChangesAsync(token);

        return true;
    }
}
