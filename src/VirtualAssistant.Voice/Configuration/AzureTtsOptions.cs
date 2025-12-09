namespace Olbrasoft.VirtualAssistant.Voice.Configuration;

/// <summary>
/// Configuration options for Azure Cognitive Services Speech TTS provider.
/// </summary>
public sealed class AzureTtsOptions
{
    public const string SectionName = "AzureTts";

    /// <summary>
    /// Azure Speech Services subscription key.
    /// Can also be set via environment variable AZURE_SPEECH_KEY.
    /// </summary>
    public string SubscriptionKey { get; set; } = string.Empty;

    /// <summary>
    /// Azure Speech Services region (e.g., "westeurope", "eastus").
    /// Can also be set via environment variable AZURE_SPEECH_REGION.
    /// Default: westeurope.
    /// </summary>
    public string Region { get; set; } = "westeurope";

    /// <summary>
    /// Voice name for synthesis (e.g., "cs-CZ-AntoninNeural" for Czech male).
    /// Can also be set via environment variable AZURE_SPEECH_VOICE.
    /// Default: cs-CZ-AntoninNeural (Czech male, neural voice).
    /// </summary>
    public string Voice { get; set; } = "cs-CZ-AntoninNeural";

    /// <summary>
    /// Output audio format.
    /// Default: Audio24Khz48KBitRateMonoMp3 (good quality, reasonable size).
    /// </summary>
    public string OutputFormat { get; set; } = "Audio24Khz48KBitRateMonoMp3";
}
