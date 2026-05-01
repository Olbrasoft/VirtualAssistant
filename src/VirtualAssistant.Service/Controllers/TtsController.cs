using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Olbrasoft.VirtualAssistant.Service.Infrastructure;

namespace Olbrasoft.VirtualAssistant.Service.Controllers;

/// <summary>
/// Legacy REST API controller for direct text-to-speech.
/// Note: For notifications, use NotificationsController at /api/notifications instead.
/// This controller provides direct TTS without database storage or batching.
/// </summary>
[ApiController]
[Route("api/tts")]
[Produces("application/json")]
public class TtsController : ControllerBase
{
    private readonly ILogger<TtsController> _logger;
    private readonly string _piperModelPath;

    public TtsController(
        ILogger<TtsController> logger,
        IConfiguration configuration)
    {
        _logger = logger;

        // Piper model path - resolve relative path
        var piperPath = configuration["TTS:Piper:ModelPath"] ?? "piper-voices/cs/cs_CZ-jirka-medium.onnx";
        _piperModelPath = Path.IsPathRooted(piperPath)
            ? piperPath
            : Path.Combine(AppContext.BaseDirectory, piperPath);
    }

    // Note: /api/tts/notify endpoint removed - use /api/notifications instead
    // The NotificationsController stores to database and uses batching/queue

    /// <summary>
    /// Returns the current per-key usage snapshot for the Google Cloud
    /// multi-key TTS provider (round-robin state, monthly counters, last call,
    /// failure counts). Used by the /Admin/Tts dashboard for live polling.
    /// </summary>
    [HttpGet("keys-usage")]
    public ActionResult<IReadOnlyList<TtsKeyUsageDto>> GetKeysUsage(
        [FromServices] IKeysUsageReporter reporter)
    {
        var snapshots = reporter.GetKeysUsage();
        var dtos = snapshots.Select(s => new TtsKeyUsageDto
        {
            Index = s.Index,
            Name = s.Name,
            State = s.State.ToString(),
            CooldownUntilUtc = s.CooldownUntilUtc,
            MonthlyCharacterCount = s.MonthlyCharacterCount,
            MonthlyCharacterLimit = s.MonthlyCharacterLimit,
            TotalSuccesses = s.TotalSuccesses,
            TotalFailures = s.TotalFailures,
            ConsecutiveFailures = s.ConsecutiveFailures,
            LastSuccessUtc = s.LastSuccessUtc,
            LastErrorUtc = s.LastErrorUtc,
            LastErrorReason = s.LastErrorReason
        }).ToList();
        return Ok(dtos);
    }

    private void SpeakWithPiper(string text)
    {
        try
        {
            var tempWav = Path.Combine(Path.GetTempPath(), $"tts_{Guid.NewGuid():N}.wav");

            // Generate audio with Piper
            using var piperProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "piper",
                    Arguments = $"--model \"{_piperModelPath}\" --output_file \"{tempWav}\"",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            piperProcess.Start();
            piperProcess.StandardInput.WriteLine(text);
            piperProcess.StandardInput.Close();
            piperProcess.WaitForExit(30000);

            if (piperProcess.ExitCode != 0)
            {
                _logger.LogError("Piper failed with exit code {Code}", piperProcess.ExitCode);
                return;
            }

            // Play audio with aplay
            using var aplayProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "aplay",
                    Arguments = tempWav,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            aplayProcess.Start();
            aplayProcess.WaitForExit(60000);

            // Cleanup
            try
            {
                System.IO.File.Delete(tempWav);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to delete temporary WAV file: {TempWav}", tempWav);
            }

            _logger.LogInformation("Piper TTS completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Piper TTS failed");
        }
    }
}

/// <summary>
/// DTO for per-key TTS usage snapshot exposed at /api/tts/keys-usage.
/// </summary>
public sealed class TtsKeyUsageDto
{
    public int Index { get; set; }
    public required string Name { get; set; }
    public required string State { get; set; }
    public DateTime? CooldownUntilUtc { get; set; }
    public long MonthlyCharacterCount { get; set; }
    public long MonthlyCharacterLimit { get; set; }
    public long TotalSuccesses { get; set; }
    public long TotalFailures { get; set; }
    public int ConsecutiveFailures { get; set; }
    public DateTime? LastSuccessUtc { get; set; }
    public DateTime? LastErrorUtc { get; set; }
    public string? LastErrorReason { get; set; }
}

/// <summary>
/// Request model for TTS notification.
/// </summary>
public class TtsNotifyRequest
{
    /// <summary>
    /// Text to speak.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Source identifier for voice profile selection (e.g., "claude", "opencode").
    /// </summary>
    public string? Source { get; set; }
}
