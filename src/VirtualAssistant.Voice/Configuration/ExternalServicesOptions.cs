namespace Olbrasoft.VirtualAssistant.Voice.Configuration;

/// <summary>
/// Configuration options for external service endpoints. URLs must be supplied
/// via appsettings / environment — empty defaults force the consuming service
/// to bind explicit values instead of silently reaching for localhost.
/// </summary>
public class ExternalServicesOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "ExternalServices";

    /// <summary>Push-to-Talk repeat endpoint URL.</summary>
    public string PttRepeatUrl { get; set; } = string.Empty;

    /// <summary>Task dispatch endpoint URL.</summary>
    public string TaskDispatchUrl { get; set; } = string.Empty;

    /// <summary>VirtualAssistant service base URL.</summary>
    public string VirtualAssistantBaseUrl { get; set; } = string.Empty;

    /// <summary>SpeechToText service base URL.</summary>
    public string SpeechToTextBaseUrl { get; set; } = string.Empty;
}
