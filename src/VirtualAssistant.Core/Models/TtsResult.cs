namespace VirtualAssistant.Core.Models;

/// <summary>
/// Result of a TTS (Text-to-Speech) operation.
/// </summary>
/// <param name="Success">Whether the TTS operation succeeded.</param>
/// <param name="ProviderUsed">Name of the TTS provider that was used (null if failed or skipped).</param>
/// <param name="DurationMs">Duration of the TTS operation in milliseconds (null if not measured).</param>
/// <param name="Skipped">Whether the TTS was skipped (e.g., due to muting).</param>
public sealed record TtsResult(
    bool Success,
    string? ProviderUsed = null,
    int? DurationMs = null,
    bool Skipped = false
);
