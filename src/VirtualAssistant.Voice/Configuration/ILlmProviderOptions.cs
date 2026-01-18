namespace Olbrasoft.VirtualAssistant.Voice.Configuration;

/// <summary>
/// Common configuration options for LLM providers.
/// </summary>
public interface ILlmProviderOptions
{
    /// <summary>
    /// API key for the provider.
    /// </summary>
    string ApiKey { get; }

    /// <summary>
    /// API base URL.
    /// </summary>
    string BaseUrl { get; }

    /// <summary>
    /// Model name to use for corrections.
    /// </summary>
    string Model { get; }

    /// <summary>
    /// API timeout in seconds.
    /// </summary>
    int TimeoutSeconds { get; }

    /// <summary>
    /// Maximum number of tokens in response.
    /// </summary>
    int MaxTokens { get; }

    /// <summary>
    /// Temperature for text generation (0.0 - 1.0).
    /// </summary>
    double Temperature { get; }

    /// <summary>
    /// Minimum text length required before sending to LLM for correction.
    /// </summary>
    int MinTextLengthForCorrection { get; }

    /// <summary>
    /// Enable or disable sending transcriptions to LLM for correction.
    /// </summary>
    bool Enabled { get; }
}
