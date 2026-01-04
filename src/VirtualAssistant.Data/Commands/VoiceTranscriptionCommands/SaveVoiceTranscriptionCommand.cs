using Olbrasoft.Data.Cqrs;
using VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Commands.VoiceTranscriptionCommands;

/// <summary>
/// Command to save a new voice transcription record.
/// </summary>
public class SaveVoiceTranscriptionCommand : BaseCommand<VoiceTranscription>
{
    public SaveVoiceTranscriptionCommand(ICommandExecutor executor) : base(executor) { }
    public SaveVoiceTranscriptionCommand(IMediator mediator) : base(mediator) { }

    /// <summary>
    /// The transcribed text. Must not be null or empty.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// The application that was focused during dictation (optional).
    /// </summary>
    public string? SourceApp { get; set; }

    /// <summary>
    /// The recording duration in milliseconds (optional).
    /// </summary>
    public int? DurationMs { get; set; }
}
