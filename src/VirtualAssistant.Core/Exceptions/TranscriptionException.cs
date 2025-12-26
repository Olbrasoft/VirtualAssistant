namespace Olbrasoft.VirtualAssistant.Core.Exceptions;

/// <summary>
/// Exception thrown when speech-to-text transcription operations fail.
/// </summary>
/// <remarks>
/// This exception is used for failures in Whisper model loading,
/// transcription service connectivity, or transcription processing errors.
/// </remarks>
public class TranscriptionException : VirtualAssistantException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TranscriptionException"/> class.
    /// </summary>
    public TranscriptionException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TranscriptionException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public TranscriptionException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TranscriptionException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public TranscriptionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
