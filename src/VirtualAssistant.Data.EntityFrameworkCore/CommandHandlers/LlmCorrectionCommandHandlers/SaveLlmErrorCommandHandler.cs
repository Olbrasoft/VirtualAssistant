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
