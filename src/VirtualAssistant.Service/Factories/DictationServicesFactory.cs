using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Audio;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Service.Factories;

/// <summary>
/// Factory for creating dictation-specific service instances.
/// Dictation uses large-v3-turbo model for higher accuracy,
/// while continuous listener uses medium model for faster processing.
/// </summary>
public class DictationServicesFactory
{
    private readonly ILogger<AudioCaptureService> _audioCaptureLogger;
    private readonly ILogger<SpeechToTextGrpcClient> _transcriberLogger;
    private readonly ILogger<TranscriptionService> _transcriptionLogger;
    private readonly IOptions<DictationOptions> _dictationOptions;
    private readonly Olbrasoft.VirtualAssistant.Voice.Filters.ITextFilter _textFilter;
    private readonly Olbrasoft.VirtualAssistant.Voice.Services.ILlmProvider _llmProvider;

    public DictationServicesFactory(
        ILogger<AudioCaptureService> audioCaptureLogger,
        ILogger<SpeechToTextGrpcClient> transcriberLogger,
        ILogger<TranscriptionService> transcriptionLogger,
        IOptions<DictationOptions> dictationOptions,
        Olbrasoft.VirtualAssistant.Voice.Filters.ITextFilter textFilter,
        Olbrasoft.VirtualAssistant.Voice.Services.ILlmProvider llmProvider)
    {
        _audioCaptureLogger = audioCaptureLogger;
        _transcriberLogger = transcriberLogger;
        _transcriptionLogger = transcriptionLogger;
        _dictationOptions = dictationOptions;
        _textFilter = textFilter;
        _llmProvider = llmProvider;
    }

    /// <summary>
    /// Creates AudioCaptureService configured for dictation.
    /// Uses DictationOptions sample rate.
    /// </summary>
    public IAudioCaptureService CreateAudioCaptureService()
    {
        // Convert DictationOptions to ContinuousListenerOptions for AudioCaptureService
        var options = Options.Create(new ContinuousListenerOptions
        {
            SampleRate = _dictationOptions.Value.SampleRate
        });

        return new AudioCaptureService(_audioCaptureLogger, options);
    }

    /// <summary>
    /// Creates SpeechToTextGrpcClient configured for dictation.
    /// Uses large-v3-turbo model for higher accuracy.
    /// </summary>
    public ISpeechTranscriber CreateTranscriber()
    {
        return new SpeechToTextGrpcClient(
            _transcriberLogger,
            _dictationOptions.Value.WhisperLanguage,
            _dictationOptions.Value.WhisperModelPath);
    }

    /// <summary>
    /// Creates TranscriptionService for dictation.
    /// Uses dictation transcriber, text filter, and LLM provider.
    /// </summary>
    public ITranscriptionService CreateTranscriptionService()
    {
        var transcriber = CreateTranscriber();

        // Convert DictationOptions to ContinuousListenerOptions for TranscriptionService
        var options = Options.Create(new ContinuousListenerOptions
        {
            WhisperLanguage = _dictationOptions.Value.WhisperLanguage,
            WhisperModelPath = _dictationOptions.Value.WhisperModelPath
        });

        return new TranscriptionService(
            _transcriptionLogger,
            transcriber,
            options,
            _textFilter,
            _llmProvider);
    }
}
