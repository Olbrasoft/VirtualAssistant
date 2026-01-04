using Olbrasoft.Data.Cqrs;
using VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Commands.VoiceTranscriptionCommands;

/// <summary>
/// Command to save a new voice transcription record.
/// </summary>
/// <param name="Text">The transcribed text. Must not be null or empty.</param>
/// <param name="SourceApp">The application that was focused during dictation (optional).</param>
/// <param name="DurationMs">The recording duration in milliseconds (optional).</param>
public record SaveVoiceTranscriptionCommand(
    string Text,
    string? SourceApp = null,
    int? DurationMs = null
) : ICommand<VoiceTranscription>;
