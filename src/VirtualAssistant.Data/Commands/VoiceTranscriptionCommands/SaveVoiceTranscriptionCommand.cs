using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Commands.VoiceTranscriptionCommands;

/// <summary>
/// Command to save a new voice transcription to the database.
/// ProviderId is passed from factory cache - no DB query needed.
/// </summary>
/// <param name="Text">The transcribed text. Must not be null or empty.</param>
/// <param name="DurationMs">Optional audio duration in milliseconds.</param>
/// <param name="ProviderId">The ID of the STT provider that created this transcription.</param>
public record SaveVoiceTranscriptionCommand(
    string Text,
    int? DurationMs,
    int ProviderId
) : ICommand<VoiceTranscription>;
