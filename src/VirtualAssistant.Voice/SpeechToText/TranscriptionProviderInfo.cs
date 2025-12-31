namespace Olbrasoft.VirtualAssistant.Voice.SpeechToText;

/// <summary>
/// Provides information about a transcription provider.
/// </summary>
public sealed class TranscriptionProviderInfo
{
    /// <summary>
    /// Gets the provider name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets whether the provider is currently available and functional.
    /// </summary>
    public bool IsAvailable { get; init; }

    /// <summary>
    /// Gets the model name being used (e.g., "ggml-medium.bin").
    /// </summary>
    public string? ModelName { get; init; }

    /// <summary>
    /// Gets whether GPU acceleration is enabled.
    /// </summary>
    public bool GpuEnabled { get; init; }

    /// <summary>
    /// Gets the supported languages (e.g., ["cs", "en", "de"]).
    /// Empty list means all languages are supported.
    /// </summary>
    public IReadOnlyList<string> SupportedLanguages { get; init; } = [];

    /// <summary>
    /// Gets additional provider-specific information.
    /// </summary>
    public string? AdditionalInfo { get; init; }
}
