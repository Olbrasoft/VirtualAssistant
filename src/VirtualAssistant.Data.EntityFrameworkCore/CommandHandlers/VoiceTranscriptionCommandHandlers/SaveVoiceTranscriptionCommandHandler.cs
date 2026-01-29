using Olbrasoft.VirtualAssistant.Data.Commands.VoiceTranscriptionCommands;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.CommandHandlers.VoiceTranscriptionCommandHandlers;

/// <summary>
/// Handler for SaveVoiceTranscriptionCommand.
/// Saves a new voice transcription to the database.
/// ProviderId comes as parameter - no DB lookup needed here.
/// </summary>
public class SaveVoiceTranscriptionCommandHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbCommandHandler<SaveVoiceTranscriptionCommand, VoiceTranscription, VoiceTranscription>(context)
{
    protected override async Task<VoiceTranscription> GetResultToHandleAsync(SaveVoiceTranscriptionCommand command, CancellationToken token)
    {
        var transcription = new VoiceTranscription
        {
            TranscribedText = command.Text,
            AudioDurationMs = command.DurationMs,
            ProviderId = command.ProviderId,
            CreatedAt = DateTime.UtcNow
        };

        Context.VoiceTranscriptions.Add(transcription);
        await Context.SaveChangesAsync(token);
        return transcription;
    }
}
