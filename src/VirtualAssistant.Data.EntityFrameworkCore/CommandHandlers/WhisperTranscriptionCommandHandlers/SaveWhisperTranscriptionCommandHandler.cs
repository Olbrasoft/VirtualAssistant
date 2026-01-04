using Olbrasoft.VirtualAssistant.Data.Commands.WhisperTranscriptionCommands;
using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data.EntityFrameworkCore.CommandHandlers.WhisperTranscriptionCommandHandlers;

/// <summary>
/// Handler for SaveWhisperTranscriptionCommand.
/// Saves a new Whisper transcription to the database.
/// </summary>
public class SaveWhisperTranscriptionCommandHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbCommandHandler<SaveWhisperTranscriptionCommand, WhisperTranscription, WhisperTranscription>(context)
{
    protected override async Task<WhisperTranscription> GetResultToHandleAsync(SaveWhisperTranscriptionCommand command, CancellationToken token)
    {
        var transcription = new WhisperTranscription
        {
            TranscribedText = command.Text,
            AudioDurationMs = command.DurationMs
        };

        Context.WhisperTranscriptions.Add(transcription);
        await Context.SaveChangesAsync(token);
        return transcription;
    }
}
