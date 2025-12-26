namespace Olbrasoft.VirtualAssistant.Core.Exceptions;

/// <summary>
/// Exception thrown when application configuration is invalid or missing.
/// </summary>
/// <remarks>
/// This exception is used for missing configuration sections, invalid paths,
/// or misconfigured application settings.
/// </remarks>
public class ConfigurationException : VirtualAssistantException
{
    /// <summary>
    /// Gets the name of the configuration key that caused the error.
    /// </summary>
    public string? ConfigurationKey { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationException"/> class.
    /// </summary>
    public ConfigurationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ConfigurationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationException"/> class with a specified error message
    /// and configuration key.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="configurationKey">The configuration key that caused the error.</param>
    public ConfigurationException(string message, string configurationKey) : base(message)
    {
        ConfigurationKey = configurationKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ConfigurationException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationException"/> class with a specified error message,
    /// configuration key, and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="configurationKey">The configuration key that caused the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ConfigurationException(string message, string configurationKey, Exception innerException) : base(message, innerException)
    {
        ConfigurationKey = configurationKey;
    }
}
