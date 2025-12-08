using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Voice.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// TTS provider using Google Translate TTS (gTTS).
/// Uses Python gTTS library for reliable access (handles Google's anti-bot measures).
/// Czech female voice, no API key required.
/// </summary>
public sealed class GoogleTtsProvider : ITtsProvider
{
    private readonly ILogger<GoogleTtsProvider> _logger;
    private readonly GoogleTtsOptions _options;

    public GoogleTtsProvider(
        ILogger<GoogleTtsProvider> logger,
        IOptions<GoogleTtsOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public string Name => "GoogleTTS";

    /// <inheritdoc />
    public bool IsAvailable => IsGttsInstalled();

    /// <inheritdoc />
    public async Task<byte[]?> GenerateAudioAsync(string text, VoiceConfig config, CancellationToken cancellationToken)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"gtts_{Guid.NewGuid():N}.mp3");

        try
        {
            // Build gtts-cli command
            var slowFlag = _options.Slow ? "--slow" : "";
            var args = $"-l {_options.Language} {slowFlag} -o \"{tempFile}\" \"{EscapeShellArg(text)}\"";

            _logger.LogDebug("Running gTTS for text: {Text}", text.Length > 50 ? text[..50] + "..." : text);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "gtts-cli",
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();

            // Read stderr for error messages
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("gTTS failed with exit code {ExitCode}: {Error}", process.ExitCode, stderr);
                return null;
            }

            if (!File.Exists(tempFile))
            {
                _logger.LogWarning("gTTS did not create output file");
                return null;
            }

            var audioData = await File.ReadAllBytesAsync(tempFile, cancellationToken);
            _logger.LogDebug("gTTS generated {Bytes} bytes of audio", audioData.Length);

            return audioData;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error generating audio with gTTS");
            return null;
        }
        finally
        {
            // Cleanup temp file
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
        }
    }

    private static bool IsGttsInstalled()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "gtts-cli",
                    Arguments = "--version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();
            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string EscapeShellArg(string arg)
    {
        // Escape special characters for shell
        return arg.Replace("\\", "\\\\")
                  .Replace("\"", "\\\"")
                  .Replace("$", "\\$")
                  .Replace("`", "\\`");
    }
}
