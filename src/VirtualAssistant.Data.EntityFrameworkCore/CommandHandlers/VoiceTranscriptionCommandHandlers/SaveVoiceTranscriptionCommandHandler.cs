using Olbrasoft.VirtualAssistant.Data.Commands.VoiceTranscriptionCommands;
using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.CommandHandlers.VoiceTranscriptionCommandHandlers;

/// <summary>
/// Handler for SaveVoiceTranscriptionCommand.
/// Saves a new voice transcription record to the database.
/// </summary>
public class SaveVoiceTranscriptionCommandHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbCommandHandler<SaveVoiceTranscriptionCommand, VoiceTranscription, VoiceTranscription>(context)
{
    protected override async Task<VoiceTranscription> GetResultToHandleAsync(SaveVoiceTranscriptionCommand command, CancellationToken token)
    {
        var transcription = new VoiceTranscription
        {
            TranscribedText = command.Text,
            SourceApp = command.SourceApp,
            DurationMs = command.DurationMs,
            CreatedAt = DateTime.UtcNow
        };

        Context.VoiceTranscriptions.Add(transcription);
        await Context.SaveChangesAsync(token);
        return transcription;
    }
}
