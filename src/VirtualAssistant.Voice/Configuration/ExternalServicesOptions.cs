namespace Olbrasoft.VirtualAssistant.Voice.Configuration;

/// <summary>
/// Configuration options for external service endpoints. Defaults are
/// intentionally empty so that a missing config section is visible (empty
/// string returned to HttpClient surfaces as an obvious error message) rather
/// than silently reaching for <c>http://localhost:…</c>. Consumers log the
/// failure and return an error result — they do NOT crash the host. Wire up
/// <see cref="Microsoft.Extensions.Options.IValidateOptions{T}"/> at startup
/// if a deployment wants fail-fast behavior.
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
