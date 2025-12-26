namespace Olbrasoft.VirtualAssistant.Core.Exceptions;

/// <summary>
/// Exception thrown when system tray operations fail.
/// </summary>
/// <remarks>
/// This exception is used for failures in tray icon initialization,
/// D-Bus communication errors, or tray menu handler issues.
/// </remarks>
public class TrayServiceException : VirtualAssistantException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TrayServiceException"/> class.
    /// </summary>
    public TrayServiceException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayServiceException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public TrayServiceException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayServiceException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public TrayServiceException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
