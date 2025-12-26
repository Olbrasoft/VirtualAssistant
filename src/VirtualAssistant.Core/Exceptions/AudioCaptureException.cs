namespace Olbrasoft.VirtualAssistant.Core.Exceptions;

/// <summary>
/// Exception thrown when audio capture operations fail.
/// </summary>
/// <remarks>
/// This exception is used for failures in audio input device access,
/// initialization, or streaming errors.
/// </remarks>
public class AudioCaptureException : VirtualAssistantException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AudioCaptureException"/> class.
    /// </summary>
    public AudioCaptureException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioCaptureException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public AudioCaptureException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioCaptureException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public AudioCaptureException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
