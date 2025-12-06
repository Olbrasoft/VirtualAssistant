using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Voice.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// TTS provider using local Piper TTS.
/// Works offline, no internet dependency.
/// Fallback when Edge TTS is unavailable (e.g., VPN active).
/// </summary>
public sealed class PiperTtsProvider : ITtsProvider
{
    private readonly ILogger<PiperTtsProvider> _logger;
    private readonly PiperOptions _options;
    private readonly string _tempDirectory;

    public string Name => "Piper";

    public PiperTtsProvider(ILogger<PiperTtsProvider> logger, IOptions<PiperOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        _tempDirectory = Path.Combine(Path.GetTempPath(), "piper-tts");
        Directory.CreateDirectory(_tempDirectory);
    }

    /// <inheritdoc/>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        // Check if piper binary exists and model file exists
        var piperExists = File.Exists(_options.PiperPath) || IsPiperInPath();
        var modelExists = File.Exists(_options.ModelPath);

        var isAvailable = piperExists && modelExists;

        if (!isAvailable)
        {
            _logger.LogDebug("Piper not available. Binary: {PiperExists}, Model: {ModelExists}",
                piperExists, modelExists);
        }

        return Task.FromResult(isAvailable);
    }

    /// <inheritdoc/>
    public async Task<byte[]?> GenerateAudioAsync(string text, VoiceConfig voiceConfig, CancellationToken cancellationToken = default)
    {
        var outputFile = Path.Combine(_tempDirectory, $"piper-{Guid.NewGuid():N}.wav");

        try
        {
            var piperPath = File.Exists(_options.PiperPath) ? _options.PiperPath : "piper";

            // Build piper command
            // Note: Piper doesn't support prosody like Edge TTS, so voiceConfig is partially ignored
            var arguments = $"--model \"{_options.ModelPath}\" --output_file \"{outputFile}\"";

            if (!string.IsNullOrEmpty(_options.ConfigPath) && File.Exists(_options.ConfigPath))
            {
                arguments += $" --config \"{_options.ConfigPath}\"";
            }

            // Apply speech rate if supported
            if (_options.LengthScale.HasValue)
            {
                arguments += $" --length-scale {_options.LengthScale.Value:F2}";
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = piperPath,
                    Arguments = arguments,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            // Write text to stdin
            await process.StandardInput.WriteAsync(text);
            await process.StandardInput.FlushAsync();
            process.StandardInput.Close();

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync(cancellationToken);
                _logger.LogError("Piper failed with exit code {ExitCode}: {Error}",
                    process.ExitCode, error);
                return null;
            }

            if (!File.Exists(outputFile))
            {
                _logger.LogError("Piper did not create output file");
                return null;
            }

            // Piper outputs WAV, but we need to return it for playback
            // Convert to MP3 for consistency with Edge TTS cache
            var mp3File = Path.ChangeExtension(outputFile, ".mp3");
            if (await ConvertWavToMp3Async(outputFile, mp3File, cancellationToken))
            {
                var audioData = await File.ReadAllBytesAsync(mp3File, cancellationToken);

                // Cleanup temp files
                TryDeleteFile(outputFile);
                TryDeleteFile(mp3File);

                return audioData;
            }
            else
            {
                // If conversion fails, return WAV directly
                var audioData = await File.ReadAllBytesAsync(outputFile, cancellationToken);
                TryDeleteFile(outputFile);
                return audioData;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Piper audio generation failed");
            TryDeleteFile(outputFile);
            return null;
        }
    }

    private bool IsPiperInPath()
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "which",
                Arguments = "piper",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            process?.WaitForExit(1000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> ConvertWavToMp3Async(string wavFile, string mp3File, CancellationToken cancellationToken)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-y -i \"{wavFile}\" -codec:a libmp3lame -qscale:a 2 \"{mp3File}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync(cancellationToken);

            return process.ExitCode == 0 && File.Exists(mp3File);
        }
        catch
        {
            return false;
        }
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
