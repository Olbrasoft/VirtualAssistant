using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Whisper.net based speech transcription service with GPU acceleration support.
/// Supports multiple models in VRAM cache with thread-safe access.
/// </summary>
public sealed class WhisperSpeechTranscriber : ISpeechTranscriber
{
    private readonly ILogger<WhisperSpeechTranscriber> _logger;
    private readonly ContinuousListenerOptions _options;

    // Model cache: stores (factory, processor, lastUsed) for each model
    private static readonly Dictionary<string, ModelCacheEntry> _modelCache = new();
    private static readonly SemaphoreSlim _globalLock = new(1, 1);
    private bool _disposed;

    private const int SampleRate = 16000;

    public string Language => _options.WhisperLanguage;

    /// <summary>
    /// Model cache entry with factory, processor, and usage tracking.
    /// </summary>
    private sealed class ModelCacheEntry
    {
        public required WhisperFactory Factory { get; init; }
        public required WhisperProcessor Processor { get; init; }
        public required SemaphoreSlim Lock { get; init; }
        public DateTime LastUsed { get; set; }
    }

    public WhisperSpeechTranscriber(
        ILogger<WhisperSpeechTranscriber> logger,
        IOptions<ContinuousListenerOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Gets or loads a Whisper model from cache.
    /// Thread-safe lazy loading - models stay in VRAM cache until service restart.
    /// </summary>
    private async Task<ModelCacheEntry> GetOrLoadModelAsync(string modelName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            throw new ArgumentException(
                "Model name must be specified in configuration (ContinuousListener:WhisperModelPath).",
                nameof(modelName));
        }

        // Fast path: check if model is already loaded (no lock needed for read)
        if (_modelCache.TryGetValue(modelName, out var cachedEntry))
        {
            cachedEntry.LastUsed = DateTime.UtcNow;
            _logger.LogDebug("Using cached model: {ModelName}", modelName);
            return cachedEntry;
        }

        // Slow path: need to load model (acquire global lock)
        await _globalLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_modelCache.TryGetValue(modelName, out var doubleCheckEntry))
            {
                doubleCheckEntry.LastUsed = DateTime.UtcNow;
                _logger.LogDebug("Using cached model after lock: {ModelName}", modelName);
                return doubleCheckEntry;
            }

            // Get model path (resolves from configuration + WhisperModelLocator)
            var modelPath = _options.GetFullWhisperModelPath();
            _logger.LogInformation("Loading Whisper.net model: {ModelName} from {ModelPath}", modelName, modelPath);

            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException($"Whisper model not found: {modelPath}");
            }

            // Log runtime library info (only on first load)
            if (_modelCache.Count == 0)
            {
                _logger.LogInformation("Runtime library order: {Order}",
                    string.Join(", ", RuntimeOptions.RuntimeLibraryOrder));
            }

            // Create factory with GPU acceleration
            var factoryOptions = new WhisperFactoryOptions { UseGpu = _options.UseGpu };
            var factory = WhisperFactory.FromPath(modelPath, factoryOptions);

            // Log which library was loaded (only on first load)
            if (_modelCache.Count == 0)
            {
                _logger.LogInformation("Loaded runtime library: {Library}",
                    RuntimeOptions.LoadedLibrary?.ToString() ?? "Unknown");
            }

            // Create processor with optimized settings
            var language = _options.WhisperLanguage ?? "auto";
            var processor = factory.CreateBuilder()
                .WithLanguage(language)
                // Use beam search for better accuracy
                .WithBeamSearchSamplingStrategy()
                .ParentBuilder
                // Lower temperature = more deterministic output
                .WithTemperature(0.0f)
                // CRITICAL: Disable context to prevent hallucinations
                .WithNoContext()
                .Build();

            var entry = new ModelCacheEntry
            {
                Factory = factory,
                Processor = processor,
                Lock = new SemaphoreSlim(1, 1),
                LastUsed = DateTime.UtcNow
            };

            _modelCache[modelName] = entry;

            _logger.LogInformation(
                "Whisper.net model loaded successfully: {ModelName} (GPU: {Gpu}, Language: {Language}, Total cached models: {Count})",
                modelName, _options.UseGpu, language, _modelCache.Count);

            return entry;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Whisper.net model: {ModelName}", modelName);
            throw;
        }
        finally
        {
            _globalLock.Release();
        }
    }

    public async Task<TranscriptionResult> TranscribeAsync(byte[] audioData, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WhisperSpeechTranscriber));

        if (audioData == null || audioData.Length == 0)
            return new TranscriptionResult("Audio data cannot be empty");

        var startTime = DateTime.UtcNow;

        try
        {
            // Get model name from configuration
            var modelFileName = _options.WhisperModelPath;
            if (string.IsNullOrWhiteSpace(modelFileName))
            {
                throw new InvalidOperationException(
                    "WhisperModelPath is not configured. " +
                    "Please set ContinuousListener:WhisperModelPath in appsettings.json.");
            }

            // Get or load model from cache
            var modelEntry = await GetOrLoadModelAsync(modelFileName, cancellationToken);

            _logger.LogDebug("Starting transcription with model {ModelName} (audio size: {Size} bytes)",
                modelFileName, audioData.Length);

            // Thread-safe access to this specific model's processor
            await modelEntry.Lock.WaitAsync(cancellationToken);
            try
            {
                // Strip WAV header if present
                var pcmData = StripWavHeader(audioData);

                // Convert PCM to float32 samples
                var samples = ConvertPcmToFloat32(pcmData);

                var audioDuration = TimeSpan.FromSeconds(samples.Length / (float)SampleRate);
                _logger.LogDebug("Audio samples: {Count} ({Seconds:F1}s)",
                    samples.Length, audioDuration.TotalSeconds);

                // Process with Whisper.net
                var segments = new List<string>();

                await foreach (var segment in modelEntry.Processor.ProcessAsync(samples, cancellationToken))
                {
                    if (!string.IsNullOrWhiteSpace(segment.Text))
                    {
                        segments.Add(segment.Text.Trim());
                        _logger.LogDebug("Segment: {Start} -> {End}: {Text}",
                            segment.Start, segment.End, segment.Text);
                    }
                }

                var transcription = string.Join(" ", segments);

                if (string.IsNullOrWhiteSpace(transcription))
                {
                    _logger.LogWarning("Transcription result is empty (likely silence)");
                    return new TranscriptionResult("No speech detected");
                }

                var transcriptionTime = DateTime.UtcNow - startTime;
                _logger.LogInformation("Transcription successful with {ModelName}: \"{Text}\" ({Time}ms)",
                    modelFileName, transcription, transcriptionTime.TotalMilliseconds);

                return new TranscriptionResult(transcription, confidence: 1.0f);
            }
            finally
            {
                modelEntry.Lock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Transcription cancelled");
            return new TranscriptionResult("Transcription cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transcription failed");
            return new TranscriptionResult($"Transcription error: {ex.Message}");
        }
    }

    public async Task<TranscriptionResult> TranscribeAsync(Stream audioStream, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WhisperSpeechTranscriber));

        if (audioStream == null)
            return new TranscriptionResult("Audio stream cannot be null");

        // Read stream to byte array
        using var memoryStream = new MemoryStream();
        await audioStream.CopyToAsync(memoryStream, cancellationToken);
        var audioData = memoryStream.ToArray();

        return await TranscribeAsync(audioData, cancellationToken);
    }

    private static float[] ConvertPcmToFloat32(byte[] pcmData)
    {
        var samples = new float[pcmData.Length / 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short sample = BitConverter.ToInt16(pcmData, i * 2);
            samples[i] = sample / 32768.0f;
        }
        return samples;
    }

    private static byte[] StripWavHeader(byte[] audioData)
    {
        if (audioData.Length > 44 &&
            audioData[0] == 'R' && audioData[1] == 'I' &&
            audioData[2] == 'F' && audioData[3] == 'F')
        {
            var pcmData = new byte[audioData.Length - 44];
            Array.Copy(audioData, 44, pcmData, 0, pcmData.Length);
            return pcmData;
        }
        return audioData;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        // Note: We don't dispose cached models (_modelCache)
        // because they're static and shared across instances.
        // They will be disposed when the app shuts down.

        _disposed = true;
        _logger.LogDebug("WhisperSpeechTranscriber disposed");
    }
}
