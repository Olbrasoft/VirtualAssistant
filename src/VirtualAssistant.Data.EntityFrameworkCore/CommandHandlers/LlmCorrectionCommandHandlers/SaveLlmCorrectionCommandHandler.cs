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
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.VoiceTranscriptionId, nameof(command.VoiceTranscriptionId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.DurationMs, nameof(command.DurationMs));
        ArgumentException.ThrowIfNullOrWhiteSpace(command.CorrectedText, nameof(command.CorrectedText));

        var correction = new LlmCorrection
        {
            VoiceTranscriptionId = command.VoiceTranscriptionId,
            CorrectedText = command.CorrectedText,
            DurationMs = command.DurationMs,
            PromptId = command.PromptId,
            ModelId = command.ModelId,
            InputTokens = command.InputTokens,
            OutputTokens = command.OutputTokens,
            ReasoningTokens = command.ReasoningTokens,
            IsWinner = command.IsWinner,
            RaceGroupId = command.RaceGroupId,
            CreatedAt = System.DateTime.UtcNow
        };

        Context.LlmCorrections.Add(correction);
        await Context.SaveChangesAsync(token);
        return correction;
    }
}
