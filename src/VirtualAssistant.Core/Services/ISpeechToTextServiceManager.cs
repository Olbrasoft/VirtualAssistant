namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Interface for managing SpeechToText microservice lifecycle.
/// </summary>
public interface ISpeechToTextServiceManager
{
    /// <summary>
    /// Checks if SpeechToText service is running.
    /// </summary>
    Task<bool> IsRunningAsync();

    /// <summary>
    /// Gets SpeechToText service version from deployed binary.
    /// </summary>
    string GetVersion();

    /// <summary>
    /// Starts SpeechToText service.
    /// </summary>
    Task<bool> StartAsync();

    /// <summary>
    /// Stops SpeechToText service.
    /// </summary>
    Task<bool> StopAsync();
}
