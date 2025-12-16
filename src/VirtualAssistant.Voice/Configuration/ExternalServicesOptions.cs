namespace Olbrasoft.VirtualAssistant.Voice.Configuration;

/// <summary>
/// Configuration options for external service endpoints.
/// Allows all service URLs to be configured via appsettings.
/// </summary>
public class ExternalServicesOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "ExternalServices";

    /// <summary>
    /// URL of the Push-to-Talk repeat endpoint.
    /// Used to repeat the last spoken text.
    /// </summary>
    public string PttRepeatUrl { get; set; } = "http://localhost:5050/api/ptt/repeat";

    /// <summary>
    /// URL of the task dispatch endpoint.
    /// Used to dispatch tasks to agents.
    /// </summary>
    public string TaskDispatchUrl { get; set; } = "http://localhost:5055/api/hub/dispatch-task";

    /// <summary>
    /// URL of the VirtualAssistant service base URL.
    /// </summary>
    public string VirtualAssistantBaseUrl { get; set; } = "http://localhost:5055";

    /// <summary>
    /// URL of the SpeechToText service base URL.
    /// </summary>
    public string SpeechToTextBaseUrl { get; set; } = "http://localhost:5050";
}
