using System.Diagnostics;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Service for capturing audio output (loopback) from PipeWire monitor source.
/// Used as reference signal for Acoustic Echo Cancellation (AEC).
/// </summary>
public class LoopbackCaptureService : IDisposable
{
    private readonly ILogger<LoopbackCaptureService> _logger;
    private readonly ContinuousListenerOptions _options;
    private Process? _process;
    private Stream? _audioStream;
    private bool _disposed;
    private readonly object _lock = new();
    private readonly Queue<byte[]> _bufferQueue = new();
    private const int MaxQueueSize = 100; // ~3.2 seconds at 32ms chunks

    /// <summary>
    /// PipeWire monitor source name for loopback capture.
    /// Can be configured or auto-detected.
    /// </summary>
    public string? MonitorSource { get; set; }

    public LoopbackCaptureService(ILogger<LoopbackCaptureService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _options = new ContinuousListenerOptions();
        configuration.GetSection(ContinuousListenerOptions.SectionName).Bind(_options);
    }

    /// <summary>
    /// Starts loopback audio capture from PipeWire monitor source.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_process != null)
        {
            _logger.LogWarning("Loopback capture already started");
            return;
        }

        // Auto-detect monitor source if not specified
        if (string.IsNullOrEmpty(MonitorSource))
        {
            MonitorSource = await DetectMonitorSourceAsync(cancellationToken);
            if (string.IsNullOrEmpty(MonitorSource))
            {
                _logger.LogError("No monitor source found. AEC will be disabled.");
                return;
            }
        }

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "pw-record",
                Arguments = $"--target \"{MonitorSource}\" --format s16 --rate {_options.SampleRate} --channels 1 -",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        _process.Start();
        _audioStream = _process.StandardOutput.BaseStream;
        _logger.LogInformation("Loopback capture started from {MonitorSource} (sample rate: {SampleRate} Hz)", 
            MonitorSource, _options.SampleRate);

        // Start background task to continuously read loopback audio
        _ = Task.Run(() => ReadLoopbackContinuouslyAsync(cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Detects the PipeWire/PulseAudio monitor source for speaker output.
    /// </summary>
    private async Task<string?> DetectMonitorSourceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pactl",
                    Arguments = "list sources short",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            // Find monitor source (e.g., "alsa_output.pci-0000_0a_00.4.playback.0.0.monitor")
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && parts[1].Contains(".monitor"))
                {
                    _logger.LogInformation("Detected monitor source: {Source}", parts[1]);
                    return parts[1];
                }
            }

            _logger.LogWarning("No monitor source detected in pactl output");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect monitor source");
            return null;
        }
    }

    /// <summary>
    /// Continuously reads loopback audio in background and buffers it.
    /// </summary>
    private async Task ReadLoopbackContinuouslyAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _audioStream != null)
            {
                var buffer = new byte[_options.ChunkSizeBytes];
                int totalRead = 0;

                while (totalRead < buffer.Length)
                {
                    int read = await _audioStream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken);
                    if (read == 0) return; // End of stream
                    totalRead += read;
                }

                lock (_lock)
                {
                    // Keep buffer queue bounded
                    while (_bufferQueue.Count >= MaxQueueSize)
                    {
                        _bufferQueue.Dequeue();
                    }
                    _bufferQueue.Enqueue(buffer);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in loopback capture background task");
        }
    }

    /// <summary>
    /// Gets the latest loopback audio chunk (reference signal for AEC).
    /// Returns null if no data available.
    /// </summary>
    public byte[]? GetLatestChunk()
    {
        lock (_lock)
        {
            // Return the most recent chunk, clearing old ones
            while (_bufferQueue.Count > 1)
            {
                _bufferQueue.Dequeue();
            }
            
            return _bufferQueue.Count > 0 ? _bufferQueue.Dequeue() : null;
        }
    }

    /// <summary>
    /// Gets whether loopback capture is active.
    /// </summary>
    public bool IsActive => _process != null && !_process.HasExited;

    /// <summary>
    /// Stops loopback audio capture.
    /// </summary>
    public void Stop()
    {
        if (_process != null && !_process.HasExited)
        {
            try
            {
                _process.Kill();
                _logger.LogInformation("Loopback capture stopped");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping loopback capture");
            }
        }

        _audioStream = null;
        _process?.Dispose();
        _process = null;

        lock (_lock)
        {
            _bufferQueue.Clear();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
