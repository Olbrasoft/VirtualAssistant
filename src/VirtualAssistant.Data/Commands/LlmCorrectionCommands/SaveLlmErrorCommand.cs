using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Commands.LlmCorrectionCommands;

/// <summary>
/// Command to save an LLM error to the database.
/// </summary>
/// <param name="VoiceTranscriptionId">ID of the voice transcription that failed correction. Must be greater than 0.</param>
/// <param name="ErrorMessage">The error message. Must not be null or empty.</param>
/// <param name="DurationMs">API call duration in milliseconds (how long before it failed). Must be greater than 0.</param>
public record SaveLlmErrorCommand(
    int VoiceTranscriptionId,
    string ErrorMessage,
    int DurationMs
) : ICommand<LlmError>;
