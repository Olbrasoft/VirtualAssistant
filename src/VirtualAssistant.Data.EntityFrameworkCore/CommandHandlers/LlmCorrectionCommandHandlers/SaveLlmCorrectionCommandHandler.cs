using Olbrasoft.VirtualAssistant.Data.Commands.LlmCorrectionCommands;
using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data.EntityFrameworkCore.CommandHandlers.LlmCorrectionCommandHandlers;

/// <summary>
/// Handler for SaveLlmCorrectionCommand.
/// Saves a successful LLM correction to the database.
/// </summary>
public class SaveLlmCorrectionCommandHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbCommandHandler<SaveLlmCorrectionCommand, LlmCorrection, LlmCorrection>(context)
{
    protected override async Task<LlmCorrection> GetResultToHandleAsync(SaveLlmCorrectionCommand command, CancellationToken token)
    {
        var correction = new LlmCorrection
        {
            WhisperTranscriptionId = command.WhisperTranscriptionId,
            CorrectedText = command.CorrectedText,
            DurationMs = command.DurationMs,
            CreatedAt = System.DateTime.UtcNow
        };

        Context.LlmCorrections.Add(correction);
        await Context.SaveChangesAsync(token);
        return correction;
    }
}
