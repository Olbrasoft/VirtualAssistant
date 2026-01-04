using Olbrasoft.Data.Cqrs;
using VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Commands.LlmCorrectionCommands;

/// <summary>
/// Command to save an LLM error to the database.
/// </summary>
/// <param name="WhisperTranscriptionId">ID of the Whisper transcription that failed correction.</param>
/// <param name="ErrorMessage">The error message.</param>
/// <param name="DurationMs">API call duration in milliseconds (how long before it failed).</param>
public record SaveLlmErrorCommand(
    int WhisperTranscriptionId,
    string ErrorMessage,
    int DurationMs
) : ICommand<LlmError>;
