using Olbrasoft.VirtualAssistant.Data.Commands.LlmCorrectionCommands;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.CommandHandlers.LlmCorrectionCommandHandlers;

/// <summary>
/// Handler for SaveLlmErrorCommand.
/// Saves an LLM error to the database.
/// </summary>
public class SaveLlmErrorCommandHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbCommandHandler<SaveLlmErrorCommand, LlmError, LlmError>(context)
{
    protected override async Task<LlmError> GetResultToHandleAsync(SaveLlmErrorCommand command, CancellationToken token)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.VoiceTranscriptionId, nameof(command.VoiceTranscriptionId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.DurationMs, nameof(command.DurationMs));
        ArgumentException.ThrowIfNullOrWhiteSpace(command.ErrorMessage, nameof(command.ErrorMessage));

        var error = new LlmError
        {
            VoiceTranscriptionId = command.VoiceTranscriptionId,
            ErrorMessage = command.ErrorMessage,
            DurationMs = command.DurationMs,
            CreatedAt = DateTime.UtcNow
        };

        Context.LlmErrors.Add(error);
        await Context.SaveChangesAsync(token);
        return error;
    }
}
