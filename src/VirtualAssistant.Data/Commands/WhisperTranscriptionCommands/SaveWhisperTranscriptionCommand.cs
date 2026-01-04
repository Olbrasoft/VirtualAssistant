using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Commands.WhisperTranscriptionCommands;

/// <summary>
/// Command to save a new Whisper transcription to the database.
/// </summary>
public class SaveWhisperTranscriptionCommand : BaseCommand<WhisperTranscription>
{
    public SaveWhisperTranscriptionCommand(ICommandExecutor executor) : base(executor) { }
    public SaveWhisperTranscriptionCommand(IMediator mediator) : base(mediator) { }

    /// <summary>
    /// The transcribed text. Must not be null or empty.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Optional audio duration in milliseconds.
    /// </summary>
    public int? DurationMs { get; set; }
}
