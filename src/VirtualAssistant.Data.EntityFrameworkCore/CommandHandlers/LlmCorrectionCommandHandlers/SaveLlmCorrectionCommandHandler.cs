using Olbrasoft.VirtualAssistant.Data.Commands.LlmCorrectionCommands;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.CommandHandlers.LlmCorrectionCommandHandlers;

/// <summary>
/// Handler for SaveLlmCorrectionCommand.
/// Saves a successful LLM correction to the database.
/// </summary>
public class SaveLlmCorrectionCommandHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbCommandHandler<SaveLlmCorrectionCommand, LlmCorrection, LlmCorrection>(context)
{
    protected override async Task<LlmCorrection> GetResultToHandleAsync(SaveLlmCorrectionCommand command, CancellationToken token)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.WhisperTranscriptionId, nameof(command.WhisperTranscriptionId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.DurationMs, nameof(command.DurationMs));
        ArgumentException.ThrowIfNullOrWhiteSpace(command.CorrectedText, nameof(command.CorrectedText));

        var correction = new LlmCorrection
        {
            WhisperTranscriptionId = command.WhisperTranscriptionId,
            CorrectedText = command.CorrectedText,
            DurationMs = command.DurationMs,
            PromptId = command.PromptId, // NULL indicates no prompt tracking (e.g., legacy code or system without desktop monitoring)
            CreatedAt = System.DateTime.UtcNow
        };

        Context.LlmCorrections.Add(correction);
        await Context.SaveChangesAsync(token);
        return correction;
    }
}
