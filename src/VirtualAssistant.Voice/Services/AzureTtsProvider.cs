using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Voice.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// TTS provider using Azure Cognitive Services Speech.
/// Primary provider with high-quality neural voices.
/// Czech male voice: cs-CZ-AntoninNeural.
/// </summary>
public sealed class AzureTtsProvider : ITtsProvider, IDisposable
{
    private readonly ILogger<AzureTtsProvider> _logger;
    private readonly AzureTtsOptions _options;
    private readonly string _subscriptionKey;
    private readonly string _region;
    private SpeechConfig? _speechConfig;
    private bool _isConfigured;
    private bool _disposed;

    public AzureTtsProvider(
        ILogger<AzureTtsProvider> logger,
        IOptions<AzureTtsOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        // Get subscription key from options or environment variable
        _subscriptionKey = !string.IsNullOrEmpty(_options.SubscriptionKey)
            ? _options.SubscriptionKey
            : Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY") ?? string.Empty;

        // Get region from options or environment variable
        _region = !string.IsNullOrEmpty(_options.Region) && _options.Region != "westeurope"
            ? _options.Region
            : Environment.GetEnvironmentVariable("AZURE_SPEECH_REGION") ?? _options.Region;

        // Validate configuration
        _isConfigured = !string.IsNullOrEmpty(_subscriptionKey) && !string.IsNullOrEmpty(_region);

        if (_isConfigured)
        {
            try
            {
                _speechConfig = SpeechConfig.FromSubscription(_subscriptionKey, _region);

                // Get voice from options or environment variable
                var voice = !string.IsNullOrEmpty(_options.Voice)
                    ? _options.Voice
                    : Environment.GetEnvironmentVariable("AZURE_SPEECH_VOICE") ?? "cs-CZ-AntoninNeural";

                _speechConfig.SpeechSynthesisVoiceName = voice;

                // Set output format
                _speechConfig.SetSpeechSynthesisOutputFormat(GetOutputFormat(_options.OutputFormat));

                _logger.LogInformation("Azure TTS configured: region={Region}, voice={Voice}",
                    _region, voice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to configure Azure Speech SDK");
                _isConfigured = false;
            }
        }
        else
        {
            _logger.LogWarning("Azure TTS not configured - missing AZURE_SPEECH_KEY or AZURE_SPEECH_REGION");
        }
    }

    /// <inheritdoc />
    public string Name => "AzureTTS";

    /// <inheritdoc />
    public bool IsAvailable => _isConfigured && _speechConfig != null;

    /// <inheritdoc />
    public async Task<byte[]?> GenerateAudioAsync(string text, VoiceConfig config, CancellationToken cancellationToken)
    {
        if (!IsAvailable || _speechConfig == null)
        {
            _logger.LogWarning("Azure TTS not available - skipping");
            return null;
        }

        try
        {
            // Create SSML with voice config (rate, pitch, volume)
            var ssml = CreateSsml(text, config);

            _logger.LogDebug("Azure TTS generating audio for: {Text}",
                text.Length > 50 ? text[..50] + "..." : text);
            _logger.LogTrace("Azure TTS SSML: {Ssml}", ssml);

            // Use pull audio output stream to get raw audio bytes
            using var audioStream = AudioOutputStream.CreatePullStream();
            using var audioConfig = AudioConfig.FromStreamOutput(audioStream);
            using var synthesizer = new SpeechSynthesizer(_speechConfig, audioConfig);

            // Synthesize with SSML
            using var result = await synthesizer.SpeakSsmlAsync(ssml);

            if (result.Reason == ResultReason.SynthesizingAudioCompleted)
            {
                var audioData = result.AudioData;
                _logger.LogDebug("Azure TTS generated {Bytes} bytes of audio", audioData.Length);
                return audioData;
            }

            if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);

                if (cancellation.Reason == CancellationReason.Error)
                {
                    _logger.LogError("Azure TTS error: {ErrorCode} - {ErrorDetails}",
                        cancellation.ErrorCode, cancellation.ErrorDetails);
                }
                else
                {
                    _logger.LogWarning("Azure TTS cancelled: {Reason}", cancellation.Reason);
                }

                return null;
            }

            _logger.LogWarning("Azure TTS unexpected result: {Reason}", result.Reason);
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating audio with Azure TTS");
            return null;
        }
    }

    /// <summary>
    /// Creates SSML markup with voice configuration.
    /// </summary>
    private string CreateSsml(string text, VoiceConfig config)
    {
        // Parse rate (e.g., "+20%" -> "20%", "-10%" -> "-10%")
        var rate = ParseProsodyValue(config.Rate, "0%");

        // Parse pitch (e.g., "+0Hz" -> "default", "-5st" -> "-5st")
        var pitch = ParseProsodyValue(config.Pitch, "default");

        // Parse volume (e.g., "+0%" -> "default")
        var volume = ParseVolumeValue(config.Volume);

        // Escape XML special characters in text
        var escapedText = System.Security.SecurityElement.Escape(text) ?? text;

        // Get voice from config or use default
        var voice = !string.IsNullOrEmpty(config.Voice)
            ? config.Voice
            : _speechConfig?.SpeechSynthesisVoiceName ?? "cs-CZ-AntoninNeural";

        return $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='cs-CZ'>
    <voice name='{voice}'>
        <prosody rate='{rate}' pitch='{pitch}' volume='{volume}'>
            {escapedText}
        </prosody>
    </voice>
</speak>";
    }

    private static string ParseProsodyValue(string value, string defaultValue)
    {
        if (string.IsNullOrEmpty(value))
            return defaultValue;

        // Check if it's a zero/default value (with or without + sign)
        var cleaned = value.TrimStart('+');
        if (cleaned == "0%" || cleaned == "0Hz" || cleaned == "0st")
            return "default";

        // Keep the original value - Azure SSML requires +20% format for relative values
        // Only return the cleaned version if it was already positive
        return value;
    }

    private static string ParseVolumeValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "default";

        var cleaned = value.TrimStart('+');

        // Convert percentage to SSML volume values
        if (cleaned == "0%")
            return "default";

        // SSML supports: silent, x-soft, soft, medium, loud, x-loud, or +/-dB
        // For percentage values, we'll convert roughly
        if (cleaned.EndsWith('%'))
        {
            if (int.TryParse(cleaned.TrimEnd('%'), out var percent))
            {
                if (percent <= -50) return "silent";
                if (percent <= -25) return "x-soft";
                if (percent <= -10) return "soft";
                if (percent <= 10) return "medium";
                if (percent <= 25) return "loud";
                return "x-loud";
            }
        }

        return "default";
    }

    private static SpeechSynthesisOutputFormat GetOutputFormat(string format)
    {
        return format switch
        {
            "Audio16Khz32KBitRateMonoMp3" => SpeechSynthesisOutputFormat.Audio16Khz32KBitRateMonoMp3,
            "Audio16Khz64KBitRateMonoMp3" => SpeechSynthesisOutputFormat.Audio16Khz64KBitRateMonoMp3,
            "Audio24Khz48KBitRateMonoMp3" => SpeechSynthesisOutputFormat.Audio24Khz48KBitRateMonoMp3,
            "Audio24Khz96KBitRateMonoMp3" => SpeechSynthesisOutputFormat.Audio24Khz96KBitRateMonoMp3,
            "Audio48Khz96KBitRateMonoMp3" => SpeechSynthesisOutputFormat.Audio48Khz96KBitRateMonoMp3,
            "Riff16Khz16BitMonoPcm" => SpeechSynthesisOutputFormat.Riff16Khz16BitMonoPcm,
            "Riff24Khz16BitMonoPcm" => SpeechSynthesisOutputFormat.Riff24Khz16BitMonoPcm,
            _ => SpeechSynthesisOutputFormat.Audio24Khz48KBitRateMonoMp3
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // SpeechConfig doesn't implement IDisposable
    }
}
