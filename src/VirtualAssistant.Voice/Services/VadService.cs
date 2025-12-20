using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Voice.Audio;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Voice Activity Detection service using Silero VAD neural network model.
/// Much more accurate than RMS-based detection, no calibration needed.
/// </summary>
public class VadService : IDisposable
{
    private readonly ILogger<VadService> _logger;
    private readonly ContinuousListenerOptions _options;
    private readonly SileroVadOnnxModel _model;
    
    // Silero VAD threshold (0.0 - 1.0)
    // 0.5 is recommended default, higher = less sensitive
    private const float SpeechThreshold = 0.5f;
    
    // Silero requires exactly 512 samples at 16kHz
    private const int SileroChunkSamples = 512;
    
    // Buffer for accumulating samples when chunks are different size
    private readonly List<float> _sampleBuffer = new();

    public VadService(ILogger<VadService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _options = new ContinuousListenerOptions();
        configuration.GetSection(ContinuousListenerOptions.SectionName).Bind(_options);

        // Load Silero VAD model - resolve relative path
        // If relative, models are in ../models/ (outside app/ directory)
        var modelPath = _options.SileroVadModelPath;
        if (!Path.IsPathRooted(modelPath))
        {
            modelPath = Path.Combine(AppContext.BaseDirectory, "..", modelPath);
        }

        _logger.LogInformation("Loading Silero VAD model from: {Path}", modelPath);

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"Silero VAD model not found at: {modelPath}");
        }

        _model = new SileroVadOnnxModel(modelPath);
        _logger.LogInformation("Silero VAD model loaded successfully");
    }

    /// <summary>
    /// Converts 16-bit PCM audio to float array normalized to -1.0 to 1.0.
    /// </summary>
    private static float[] PcmToFloat(byte[] pcmData)
    {
        int sampleCount = pcmData.Length / 2;
        float[] samples = new float[sampleCount];
        
        for (int i = 0; i < sampleCount; i++)
        {
            short sample = BitConverter.ToInt16(pcmData, i * 2);
            samples[i] = sample / 32768.0f;
        }
        
        return samples;
    }

    /// <summary>
    /// Detects if the audio chunk contains speech using Silero VAD.
    /// </summary>
    /// <param name="pcmData">16-bit PCM audio data at 16kHz.</param>
    /// <returns>True if speech detected, false otherwise.</returns>
    public bool IsSpeech(byte[] pcmData)
    {
        var (isSpeech, _) = Analyze(pcmData);
        return isSpeech;
    }

    /// <summary>
    /// Detects if the audio chunk contains speech and returns the probability.
    /// </summary>
    /// <param name="pcmData">16-bit PCM audio data at 16kHz.</param>
    /// <returns>Tuple of (isSpeech, probability 0.0-1.0).</returns>
    public (bool IsSpeech, float Probability) Analyze(byte[] pcmData)
    {
        // Convert PCM to float samples
        var newSamples = PcmToFloat(pcmData);
        _sampleBuffer.AddRange(newSamples);
        
        // Silero requires exactly 512 samples at 16kHz
        if (_sampleBuffer.Count < SileroChunkSamples)
        {
            // Not enough samples yet, return no speech
            return (false, 0.0f);
        }
        
        // Process all complete 512-sample chunks
        float maxProbability = 0.0f;
        
        while (_sampleBuffer.Count >= SileroChunkSamples)
        {
            // Extract 512 samples
            var chunk = _sampleBuffer.Take(SileroChunkSamples).ToArray();
            _sampleBuffer.RemoveRange(0, SileroChunkSamples);
            
            // Run inference
            try
            {
                float[][] input = [chunk];
                var result = _model.Call(input, _options.SampleRate);
                
                if (result.Length > 0)
                {
                    maxProbability = Math.Max(maxProbability, result[0]);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Silero VAD inference failed");
                return (false, 0.0f);
            }
        }
        
        return (maxProbability > SpeechThreshold, maxProbability);
    }

    /// <summary>
    /// Resets the internal state of the VAD model.
    /// Call this when starting a new recording session.
    /// </summary>
    public void Reset()
    {
        _model.ResetStates();
        _sampleBuffer.Clear();
    }

    /// <summary>
    /// Gets the speech detection threshold.
    /// </summary>
    public float SpeechDetectionThreshold => SpeechThreshold;

    public void Dispose()
    {
        _model?.Dispose();
        GC.SuppressFinalize(this);
    }
}
