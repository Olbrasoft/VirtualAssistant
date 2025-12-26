namespace Olbrasoft.VirtualAssistant.Core.Exceptions;

/// <summary>
/// Base exception for all VirtualAssistant-specific exceptions.
/// Use this as the root of the exception hierarchy for domain-specific errors.
/// </summary>
public class VirtualAssistantException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualAssistantException"/> class.
    /// </summary>
    public VirtualAssistantException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualAssistantException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public VirtualAssistantException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualAssistantException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public VirtualAssistantException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
