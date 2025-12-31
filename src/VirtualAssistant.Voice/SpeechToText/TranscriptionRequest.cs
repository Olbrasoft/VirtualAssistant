using System.ComponentModel.DataAnnotations;

namespace Olbrasoft.VirtualAssistant.Voice.SpeechToText;

/// <summary>
/// Represents a speech-to-text transcription request.
/// </summary>
public sealed class TranscriptionRequest : IValidatableObject
{
    /// <summary>
    /// Maximum allowed audio size (10 MB).
    /// </summary>
    public const int MaxAudioSizeBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Gets the audio data to transcribe (PCM 16kHz mono).
    /// </summary>
    [Required(ErrorMessage = "Audio data is required")]
    public required byte[] AudioData { get; init; }

    /// <summary>
    /// Gets the language code for transcription (e.g., "cs", "en").
    /// If not specified, auto-detection is used.
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// Gets the optional preferred provider name.
    /// When set, orchestration will try this provider first before falling back to others.
    /// </summary>
    public string? PreferredProvider { get; init; }

    /// <summary>
    /// Gets the optional Whisper model name override.
    /// If not specified, the default model from configuration is used.
    /// </summary>
    public string? ModelName { get; init; }

    /// <summary>
    /// Validates the request and returns validation errors.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AudioData == null || AudioData.Length == 0)
        {
            yield return new ValidationResult(
                "Audio data cannot be empty",
                [nameof(AudioData)]);
        }

        if (AudioData?.Length > MaxAudioSizeBytes)
        {
            yield return new ValidationResult(
                $"Audio data cannot exceed {MaxAudioSizeBytes / 1024 / 1024} MB",
                [nameof(AudioData)]);
        }
    }

    /// <summary>
    /// Validates the request and throws if invalid.
    /// </summary>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    public void EnsureValid()
    {
        var context = new ValidationContext(this);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(this, context, results, validateAllProperties: true))
        {
            var errors = string.Join("; ", results.Select(r => r.ErrorMessage));
            throw new ValidationException($"Invalid transcription request: {errors}");
        }
    }

    /// <summary>
    /// Checks if the request is valid.
    /// </summary>
    /// <param name="errors">When invalid, contains the validation errors.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public bool IsValid(out IReadOnlyList<string> errors)
    {
        var context = new ValidationContext(this);
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(this, context, results, validateAllProperties: true);
        errors = results.Select(r => r.ErrorMessage ?? "Unknown error").ToList();
        return isValid;
    }
}
