using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Commands.LlmCorrectionCommands;

/// <summary>
/// Command to save an LLM error to the database.
/// </summary>
public class SaveLlmErrorCommand : BaseCommand<LlmError>
{
    public SaveLlmErrorCommand(ICommandExecutor executor) : base(executor) { }
    public SaveLlmErrorCommand(IMediator mediator) : base(mediator) { }

    /// <summary>
    /// ID of the Whisper transcription that failed correction. Must be greater than 0.
    /// </summary>
    public int WhisperTranscriptionId { get; set; } = -1;

    /// <summary>
    /// The error message. Must not be null or empty.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// API call duration in milliseconds (how long before it failed). Must be greater than 0.
    /// </summary>
    public int DurationMs { get; set; } = -1;
}
