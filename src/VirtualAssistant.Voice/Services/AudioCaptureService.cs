using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Service for capturing audio from PipeWire using pw-record.
/// </summary>
public class AudioCaptureService : IAudioCaptureService
{
    private readonly ILogger<AudioCaptureService> _logger;
    private readonly ContinuousListenerOptions _options;
    private Process? _process;
    private Stream? _audioStream;
    private bool _disposed;

    public AudioCaptureService(
        ILogger<AudioCaptureService> logger, 
        IConfiguration configuration)
    {
        _logger = logger;
        _options = new ContinuousListenerOptions();
        configuration.GetSection(ContinuousListenerOptions.SectionName).Bind(_options);
    }

    /// <summary>
    /// Starts audio capture from PipeWire.
    /// Uses configured AudioSource if specified, otherwise default microphone.
    /// </summary>
    public void Start()
    {
        if (_process != null)
        {
            _logger.LogWarning("Audio capture already started");
            return;
        }

        // Build pw-record arguments
        var arguments = $"--format s16 --rate {_options.SampleRate} --channels 1";
        
        // Use AEC source if configured, otherwise default microphone
        if (!string.IsNullOrEmpty(_options.AudioSource))
        {
            arguments = $"--target \"{_options.AudioSource}\" {arguments}";
            _logger.LogInformation("Using audio source: {AudioSource}", _options.AudioSource);
        }
        
        arguments += " -"; // Output to stdout

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "pw-record",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        _process.Start();
        _audioStream = _process.StandardOutput.BaseStream;
        
        _logger.LogInformation("Audio capture started (sample rate: {SampleRate} Hz)", _options.SampleRate);
    }

    /// <summary>
    /// Reads one chunk of audio data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Audio chunk data, or null if end of stream.</returns>
    public async Task<byte[]?> ReadChunkAsync(CancellationToken cancellationToken)
    {
        if (_audioStream == null)
        {
            throw new InvalidOperationException("Audio capture not started");
        }

        var buffer = new byte[_options.ChunkSizeBytes];
        int totalRead = 0;

        while (totalRead < buffer.Length)
        {
            int read = await _audioStream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken);
            if (read == 0) return null; // End of stream
            totalRead += read;
        }

        return buffer;
    }

    /// <summary>
    /// Stops audio capture.
    /// </summary>
    public void Stop()
    {
        if (_process != null && !_process.HasExited)
        {
            try
            {
                _process.Kill();
                _logger.LogInformation("Audio capture stopped");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping audio capture");
            }
        }

        _audioStream = null;
        _process?.Dispose();
        _process = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
