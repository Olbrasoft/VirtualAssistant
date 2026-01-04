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
        // TranscriptionCorrection doesn't have UsageCount or LastUsedAt properties
        // This command is designed for future analytics feature
        // For now, just verify the correction exists
        var exists = await Context.TranscriptionCorrections
            .AnyAsync(c => c.Id == command.CorrectionId, token);

        return exists;
    }
}
