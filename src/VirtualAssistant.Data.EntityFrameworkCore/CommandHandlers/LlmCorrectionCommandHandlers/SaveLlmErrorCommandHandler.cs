using Olbrasoft.VirtualAssistant.Data.Commands.LlmCorrectionCommands;
using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data.EntityFrameworkCore.CommandHandlers.LlmCorrectionCommandHandlers;

/// <summary>
/// Handler for SaveLlmErrorCommand.
/// Saves an LLM error to the database.
/// </summary>
public class SaveLlmErrorCommandHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbCommandHandler<SaveLlmErrorCommand, LlmError, LlmError>(context)
{
    protected override async Task<LlmError> GetResultToHandleAsync(SaveLlmErrorCommand command, CancellationToken token)
    {
        if (command.WhisperTranscriptionId <= 0)
            throw new ArgumentException("WhisperTranscriptionId must be greater than 0", nameof(command.WhisperTranscriptionId));

        if (command.DurationMs <= 0)
            throw new ArgumentException("DurationMs must be greater than 0", nameof(command.DurationMs));

        if (string.IsNullOrWhiteSpace(command.ErrorMessage))
            throw new ArgumentException("ErrorMessage must not be null or empty", nameof(command.ErrorMessage));

        var error = new LlmError
        {
            WhisperTranscriptionId = command.WhisperTranscriptionId,
            ErrorMessage = command.ErrorMessage,
            DurationMs = command.DurationMs
        };

        Context.LlmErrors.Add(error);
        await Context.SaveChangesAsync(token);
        return error;
    }
}
