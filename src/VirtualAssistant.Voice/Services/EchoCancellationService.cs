using SpeexDSPSharp.Core;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Service for Acoustic Echo Cancellation (AEC) using SpeexDSP.
/// Removes speaker output (TTS) from microphone input before sending to Whisper.
/// </summary>
public class EchoCancellationService : IDisposable
{
    private readonly ILogger<EchoCancellationService> _logger;
    private readonly ContinuousListenerOptions _options;
    private readonly LoopbackCaptureService _loopbackService;
    private SpeexDSPEchoCanceler? _echoCanceler;
    private bool _disposed;
    private bool _isEnabled;

    /// <summary>
    /// Frame size in samples (must match audio chunk size).
    /// </summary>
    private int FrameSize => _options.SampleRate * _options.VadChunkMs / 1000;

    /// <summary>
    /// Filter length in samples. Longer = better cancellation but more CPU.
    /// Typical: 100-500ms worth of samples.
    /// </summary>
    private int FilterLength => _options.SampleRate * 200 / 1000; // 200ms

    public EchoCancellationService(
        ILogger<EchoCancellationService> logger, 
        IConfiguration configuration,
        LoopbackCaptureService loopbackService)
    {
        _logger = logger;
        _loopbackService = loopbackService;
        _options = new ContinuousListenerOptions();
        configuration.GetSection(ContinuousListenerOptions.SectionName).Bind(_options);
    }

    /// <summary>
    /// Initializes the echo canceler. Must be called before processing.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Start loopback capture first
            await _loopbackService.StartAsync(cancellationToken);
            
            if (!_loopbackService.IsActive)
            {
                _logger.LogWarning("Loopback capture not available. AEC will be disabled.");
                _isEnabled = false;
                return;
            }

            // Initialize SpeexDSP echo canceler
            _echoCanceler = new SpeexDSPEchoCanceler(FrameSize, FilterLength);
            
            var sampleRate = _options.SampleRate;
            _echoCanceler.Ctl(EchoCancellationCtl.SPEEX_ECHO_SET_SAMPLING_RATE, ref sampleRate);

            _isEnabled = true;
            _logger.LogInformation(
                "Echo cancellation initialized (frame: {FrameSize} samples, filter: {FilterLength} samples, rate: {SampleRate} Hz)",
                FrameSize, FilterLength, _options.SampleRate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize echo cancellation. AEC will be disabled.");
            _isEnabled = false;
        }
    }

    /// <summary>
    /// Processes a microphone audio chunk, removing echo from speaker output.
    /// </summary>
    /// <param name="microphoneChunk">Raw audio from microphone (16-bit PCM).</param>
    /// <returns>Echo-cancelled audio chunk, or original if AEC is disabled.</returns>
    public byte[] ProcessChunk(byte[] microphoneChunk)
    {
        if (!_isEnabled || _echoCanceler == null)
        {
            return microphoneChunk;
        }

        try
        {
            // Get the latest reference signal from loopback
            var referenceChunk = _loopbackService.GetLatestChunk();
            
            if (referenceChunk == null || referenceChunk.Length != microphoneChunk.Length)
            {
                // No reference available - return original (silence in speakers)
                return microphoneChunk;
            }

            // Feed the reference signal (what's playing through speakers)
            _echoCanceler.EchoPlayback(referenceChunk);

            // Apply echo cancellation to microphone input
            var outputChunk = new byte[microphoneChunk.Length];
            _echoCanceler.EchoCapture(microphoneChunk, outputChunk);

            return outputChunk;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during echo cancellation, returning original audio");
            return microphoneChunk;
        }
    }

    /// <summary>
    /// Gets whether AEC is currently enabled and active.
    /// </summary>
    public bool IsEnabled => _isEnabled;

    public void Dispose()
    {
        if (_disposed) return;
        
        _echoCanceler?.Dispose();
        _echoCanceler = null;
        _isEnabled = false;
        _disposed = true;
        
        GC.SuppressFinalize(this);
    }
}
