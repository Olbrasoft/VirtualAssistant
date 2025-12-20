using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using VirtualAssistant.Data.Dtos.Common;

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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TtsController> _logger;
    private readonly string _edgeTtsServerUrl;
    private readonly string _piperModelPath;

    public TtsController(
        IHttpClientFactory httpClientFactory,
        ILogger<TtsController> logger,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        // Read from config with fallback defaults
        var edgeTtsBaseUrl = configuration["EdgeTtsServer:BaseUrl"] ?? "http://localhost:5555";
        _edgeTtsServerUrl = $"{edgeTtsBaseUrl}/api/speech/speak";

        // Piper model path - resolve relative path
        var piperPath = configuration["TTS:Piper:ModelPath"] ?? "piper-voices/cs/cs_CZ-jirka-medium.onnx";
        _piperModelPath = Path.IsPathRooted(piperPath)
            ? piperPath
            : Path.Combine(AppContext.BaseDirectory, piperPath);
    }

    // Note: /api/tts/notify endpoint removed - use /api/notifications instead
    // The NotificationsController stores to database and uses batching/queue

    private async Task<bool> TryEdgeTtsAsync(string text, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            var ttsRequest = new
            {
                text,
                voice = "cs-CZ-AntoninNeural",
                rate = "+5%",
                volume = "+0%",
                pitch = "-5st"
            };

            var content = new StringContent(
                JsonSerializer.Serialize(ttsRequest),
                Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync(_edgeTtsServerUrl, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("EdgeTTS returned {Status}", response.StatusCode);
                return false;
            }

            var responseText = await response.Content.ReadAsStringAsync(ct);

            // Check if EdgeTTS actually succeeded (not just returned 200)
            if (responseText.Contains("\"success\":false") || responseText.Contains("Failed"))
            {
                _logger.LogWarning("EdgeTTS returned success=false: {Response}", responseText);
                return false;
            }

            _logger.LogDebug("EdgeTTS response: {Response}", responseText);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EdgeTTS failed");
            return false;
        }
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
            try { System.IO.File.Delete(tempWav); } catch { }

            _logger.LogInformation("Piper TTS completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Piper TTS failed");
        }
    }
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
