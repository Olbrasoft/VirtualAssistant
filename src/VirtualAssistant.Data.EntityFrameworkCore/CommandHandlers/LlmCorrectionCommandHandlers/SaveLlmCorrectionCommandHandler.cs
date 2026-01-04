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
        if (command.WhisperTranscriptionId <= 0)
            throw new ArgumentException("WhisperTranscriptionId must be greater than 0", nameof(command.WhisperTranscriptionId));

        if (command.DurationMs <= 0)
            throw new ArgumentException("DurationMs must be greater than 0", nameof(command.DurationMs));

        if (string.IsNullOrWhiteSpace(command.CorrectedText))
            throw new ArgumentException("CorrectedText must not be null or empty", nameof(command.CorrectedText));

        var correction = new LlmCorrection
        {
            WhisperTranscriptionId = command.WhisperTranscriptionId,
            CorrectedText = command.CorrectedText,
            DurationMs = command.DurationMs
        };

        Context.LlmCorrections.Add(correction);
        await Context.SaveChangesAsync(token);
        return correction;
    }
}
