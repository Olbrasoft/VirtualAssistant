using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Voice.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Voice configuration for different AI clients.
/// </summary>
public sealed record VoiceConfig(string Voice, string Rate, string Volume, string Pitch);

/// <summary>
/// Text-to-Speech service orchestrator.
/// Handles queue management, caching, playback, and delegates audio generation to ITtsProvider.
/// Uses single voice configuration for the Virtual Assistant.
/// </summary>
public sealed class TtsService : IDisposable
{
    private readonly ILogger<TtsService> _logger;
    private readonly ITtsProvider _ttsProvider;
    private readonly string _cacheDirectory;
    private readonly string _micLockFile = "/tmp/speech-lock";
    private readonly ConcurrentQueue<(string Text, string? Source)> _messageQueue = new();
    private readonly SemaphoreSlim _playbackLock = new(1, 1);
    private Process? _currentPlaybackProcess;

    /// <summary>
    /// Single voice configuration for the Virtual Assistant.
    /// </summary>
    private readonly VoiceConfig _voiceConfig;

    public TtsService(ILogger<TtsService> logger, IConfiguration configuration, ITtsProvider ttsProvider)
    {
        _logger = logger;
        _ttsProvider = ttsProvider;
        _cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cache", "virtual-assistant-tts");

        Directory.CreateDirectory(_cacheDirectory);

        // Load single voice config from configuration
        var options = new TtsVoiceProfilesOptions();
        configuration.GetSection(TtsVoiceProfilesOptions.SectionName).Bind(options);
        _voiceConfig = options.ToVoiceConfig();

        _logger.LogInformation("TTS Voice: {Voice}, Rate: {Rate}, Pitch: {Pitch}",
            _voiceConfig.Voice, _voiceConfig.Rate, _voiceConfig.Pitch);
        _logger.LogInformation("Using TTS provider: {Provider}", _ttsProvider.Name);
    }

    /// <summary>
    /// Gets the voice configuration (single config for all sources).
    /// </summary>
    private VoiceConfig GetVoiceConfig(string? source) => _voiceConfig;

    /// <summary>
    /// Řekne text přes TTS provider.
    /// If speech lock is active, queues the message for later playback.
    /// </summary>
    /// <param name="text">Text to speak</param>
    /// <param name="source">AI client identifier for voice selection (e.g., "opencode", "claudecode")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task SpeakAsync(string text, string? source = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if microphone is active - queue message instead of discarding
            if (File.Exists(_micLockFile))
            {
                _messageQueue.Enqueue((text, source));
                _logger.LogInformation("🎤 Mikrofon aktivní - text zařazen do fronty ({QueueCount}): {Text}",
                    _messageQueue.Count, text);
                return;
            }

            await SpeakDirectAsync(text, source, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TTS error for text: {Text}", text);
        }
    }

    /// <summary>
    /// Plays all queued messages. Called by DictationWorker after dictation ends.
    /// </summary>
    public async Task FlushQueueAsync(CancellationToken cancellationToken = default)
    {
        var count = _messageQueue.Count;
        if (count == 0)
        {
            _logger.LogDebug("FlushQueue: No messages in queue");
            return;
        }

        _logger.LogInformation("🔊 FlushQueue: Playing {Count} queued message(s)", count);

        while (_messageQueue.TryDequeue(out var item))
        {
            // Check if lock was re-acquired (user started dictating again)
            if (File.Exists(_micLockFile))
            {
                // Re-queue the message and stop flushing
                _messageQueue.Enqueue(item);
                _logger.LogInformation("🎤 Lock re-acquired during flush - stopping. Remaining: {Count}",
                    _messageQueue.Count);
                return;
            }

            await SpeakDirectAsync(item.Text, item.Source, cancellationToken);
        }

        _logger.LogDebug("FlushQueue: All messages played");
    }

    /// <summary>
    /// Gets the number of messages currently in the queue.
    /// </summary>
    public int QueueCount => _messageQueue.Count;

    /// <summary>
    /// Stops any currently playing TTS audio immediately.
    /// Called when user presses CapsLock to start recording.
    /// </summary>
    public void StopPlayback()
    {
        try
        {
            var process = _currentPlaybackProcess;
            if (process != null && !process.HasExited)
            {
                _logger.LogInformation("🛑 Stopping TTS playback (CapsLock pressed)");
                process.Kill();
                _currentPlaybackProcess = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping TTS playback");
        }
    }

    /// <summary>
    /// Internal method to actually speak text (no queue check).
    /// Uses SemaphoreSlim to ensure only one message plays at a time.
    /// </summary>
    private async Task SpeakDirectAsync(string text, string? source, CancellationToken cancellationToken)
    {
        var voiceConfig = GetVoiceConfig(source);

        await _playbackLock.WaitAsync(cancellationToken);
        try
        {
            // CRITICAL: Check lock AGAIN before any playback - user may have started recording
            if (File.Exists(_micLockFile))
            {
                _messageQueue.Enqueue((text, source));
                _logger.LogInformation("🎤 Lock detected in SpeakDirect - queuing: {Text}", text);
                return;
            }

            // Check cache first
            var cacheFile = GetCacheFilePath(text, voiceConfig);
            if (File.Exists(cacheFile))
            {
                _logger.LogDebug("Playing from cache: {Text} (source: {Source})", text, source ?? "default");

                // Check lock before playback
                if (File.Exists(_micLockFile))
                {
                    _messageQueue.Enqueue((text, source));
                    _logger.LogInformation("🎤 Lock detected before cache playback - queuing: {Text}", text);
                    return;
                }

                await PlayAudioAsync(cacheFile, cancellationToken);
                return;
            }

            // Generate new audio via provider
            _logger.LogDebug("Generating audio for: {Text} (source: {Source}, provider: {Provider})",
                text, source ?? "default", _ttsProvider.Name);
            var audioData = await _ttsProvider.GenerateAudioAsync(text, voiceConfig, cancellationToken);

            if (audioData == null || audioData.Length == 0)
            {
                _logger.LogWarning("Failed to generate audio for: {Text}", text);
                return;
            }

            // Save to cache
            await File.WriteAllBytesAsync(cacheFile, audioData, cancellationToken);

            // CRITICAL: Check lock AGAIN before playing - generation may have taken time
            if (File.Exists(_micLockFile))
            {
                _messageQueue.Enqueue((text, source));
                _logger.LogInformation("🎤 Lock detected after generation - queuing: {Text}", text);
                return;
            }

            // Play audio
            await PlayAudioAsync(cacheFile, cancellationToken);

            _logger.LogInformation("🗣️ TTS [{Source}]: \"{Text}\"", source ?? "default", text);
        }
        finally
        {
            _playbackLock.Release();
        }
    }

    private async Task PlayAudioAsync(string audioFile, CancellationToken cancellationToken)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/ffplay",
                Arguments = $"-nodisp -autoexit -loglevel quiet \"{audioFile}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        _currentPlaybackProcess = process;
        try
        {
            process.Start();

            // Monitor for speech lock while playing - check every 50ms
            while (!process.HasExited)
            {
                // Check if user started recording (CapsLock pressed)
                if (File.Exists(_micLockFile))
                {
                    _logger.LogInformation("🛑 Speech lock detected during playback - killing ffplay");
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // Process may have exited already
                    }
                    break;
                }

                // Wait 50ms before next check, or until process exits
                try
                {
                    await Task.Delay(50, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // Ignore
                    }
                    throw;
                }
            }

            // Ensure process has exited
            if (!process.HasExited)
            {
                await process.WaitForExitAsync(cancellationToken);
            }
        }
        finally
        {
            _currentPlaybackProcess = null;
            process.Dispose();  // Prevent resource leak
        }
    }

    private string GetCacheFilePath(string text, VoiceConfig config)
    {
        var safeName = new string(text
            .Take(50)
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray())
            .Trim('-')
            .ToLowerInvariant();

        var parameters = $"{config.Voice}{config.Rate}{config.Volume}{config.Pitch}";
        var hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(parameters)))[..8];

        return Path.Combine(_cacheDirectory, $"{safeName}-{hash}.mp3");
    }

    public void Dispose()
    {
        _playbackLock.Dispose();
    }
}
