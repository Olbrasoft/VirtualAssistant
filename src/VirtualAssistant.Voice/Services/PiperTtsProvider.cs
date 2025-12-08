using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Voice.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// TTS provider using Piper TTS (offline, local).
/// Czech male voice "Jirka", no network required.
/// Last resort fallback when online providers fail.
/// </summary>
public sealed class PiperTtsProvider : ITtsProvider
{
    private readonly ILogger<PiperTtsProvider> _logger;
    private readonly PiperTtsOptions _options;

    public PiperTtsProvider(
        ILogger<PiperTtsProvider> logger,
        IOptions<PiperTtsOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public string Name => "PiperTTS";

    /// <inheritdoc />
    public bool IsAvailable => IsPiperInstalled() && File.Exists(_options.ModelPath);

    /// <summary>
    /// Gets or sets the source profile key for voice configuration (e.g., "claudecode", "default").
    /// Used to select the appropriate Piper voice profile.
    /// </summary>
    public string? SourceProfile { get; set; }

    /// <inheritdoc />
    public async Task<byte[]?> GenerateAudioAsync(string text, VoiceConfig config, CancellationToken cancellationToken)
    {
        var tempWav = Path.Combine(Path.GetTempPath(), $"piper_{Guid.NewGuid():N}.wav");

        try
        {
            // Get Piper voice profile based on source
            var profileKey = SourceProfile ?? "default";
            if (!_options.Profiles.TryGetValue(profileKey, out var piperProfile))
            {
                _options.Profiles.TryGetValue("default", out piperProfile);
                piperProfile ??= new PiperVoiceConfig();
            }

            // Build Piper command arguments
            var args = $"--model \"{_options.ModelPath}\" --output_file \"{tempWav}\" " +
                $"--length-scale {piperProfile.LengthScale.ToString(CultureInfo.InvariantCulture)} " +
                $"--noise-scale {piperProfile.NoiseScale.ToString(CultureInfo.InvariantCulture)} " +
                $"--noise-w-scale {piperProfile.NoiseWScale.ToString(CultureInfo.InvariantCulture)} " +
                $"--sentence-silence {piperProfile.SentenceSilence.ToString(CultureInfo.InvariantCulture)} " +
                $"--volume {piperProfile.Volume.ToString(CultureInfo.InvariantCulture)} " +
                $"--speaker {piperProfile.Speaker}";

            _logger.LogDebug("Running Piper TTS with profile '{Profile}' for text: {Text}",
                profileKey, text.Length > 50 ? text[..50] + "..." : text);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "piper",
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();

            // Send text via stdin
            await process.StandardInput.WriteLineAsync(text);
            process.StandardInput.Close();

            // Read stderr for error messages
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("Piper failed with exit code {ExitCode}: {Error}", process.ExitCode, stderr);
                return null;
            }

            if (!File.Exists(tempWav))
            {
                _logger.LogWarning("Piper did not create output file");
                return null;
            }

            var audioData = await File.ReadAllBytesAsync(tempWav, cancellationToken);
            _logger.LogDebug("Piper generated {Bytes} bytes of audio (WAV)", audioData.Length);

            return audioData;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error generating audio with Piper");
            return null;
        }
        finally
        {
            // Cleanup temp file
            try { if (File.Exists(tempWav)) File.Delete(tempWav); } catch { }
        }
    }

    private static bool IsPiperInstalled()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "piper",
                    Arguments = "--help",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();
            process.WaitForExit(5000);
            return true; // piper --help returns non-zero but exists
        }
        catch
        {
            return false;
        }
    }
}
