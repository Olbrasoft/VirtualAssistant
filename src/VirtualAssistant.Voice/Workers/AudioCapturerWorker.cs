using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Events;
using Olbrasoft.VirtualAssistant.Voice.Audio;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Workers;

/// <summary>
/// Worker responsible for capturing audio from the microphone.
/// Publishes AudioChunkCapturedEvent for each chunk.
/// Single Responsibility: Audio I/O only.
/// </summary>
public class AudioCapturerWorker : BackgroundService
{
    private readonly ILogger<AudioCapturerWorker> _logger;
    private readonly AudioCaptureService _audioCapture;
    private readonly IEventBus _eventBus;

    public AudioCapturerWorker(
        ILogger<AudioCapturerWorker> logger,
        AudioCaptureService audioCapture,
        IEventBus eventBus)
    {
        _logger = logger;
        _audioCapture = audioCapture;
        _eventBus = eventBus;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _audioCapture.Start();
            _logger.LogInformation("AudioCapturerWorker started - capturing audio");

            while (!stoppingToken.IsCancellationRequested)
            {
                var chunk = await _audioCapture.ReadChunkAsync(stoppingToken);
                if (chunk == null)
                {
                    await Task.Delay(100, stoppingToken);
                    continue;
                }

                // Calculate RMS for VAD
                var rms = CalculateRMS(chunk);

                // Publish event
                await _eventBus.PublishAsync(new AudioChunkCapturedEvent(chunk, rms), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("AudioCapturerWorker stopped");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not started"))
        {
            _logger.LogDebug("Audio capture stopped, waiting for unmute");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in audio capture loop");
        }
        finally
        {
            _audioCapture.Stop();
            _logger.LogInformation("Audio capture stopped");
        }
    }

    private static float CalculateRMS(byte[] audioData)
    {
        // Convert bytes to 16-bit PCM samples
        long sum = 0;
        int sampleCount = audioData.Length / 2;

        for (int i = 0; i < audioData.Length - 1; i += 2)
        {
            short sample = BitConverter.ToInt16(audioData, i);
            sum += sample * sample;
        }

        return (float)Math.Sqrt(sum / (double)sampleCount);
    }
}
