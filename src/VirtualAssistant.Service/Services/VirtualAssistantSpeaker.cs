using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Voice.Services;
using VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Services;

/// <summary>
/// Single entry point for all TTS operations in VirtualAssistant.
/// This is the ONLY class that injects TtsService - all other components use IVirtualAssistantSpeaker.
/// Supports speech queue with cancellation for interruption scenarios.
/// </summary>
public class VirtualAssistantSpeaker : IVirtualAssistantSpeaker
{
    private readonly TtsService _ttsService;
    private readonly ISpeechQueueService _speechQueueService;
    private readonly ILogger<VirtualAssistantSpeaker> _logger;

    public VirtualAssistantSpeaker(
        TtsService ttsService,
        ISpeechQueueService speechQueueService,
        ILogger<VirtualAssistantSpeaker> logger)
    {
        _ttsService = ttsService;
        _speechQueueService = speechQueueService;
        _logger = logger;
    }

    /// <summary>
    /// Whether speech is currently playing.
    /// </summary>
    public bool IsSpeaking => _speechQueueService.IsSpeaking;

    /// <summary>
    /// Cancels currently playing speech.
    /// </summary>
    public void CancelCurrentSpeech()
    {
        _speechQueueService.CancelCurrent();
        _ttsService.StopPlayback();
    }

    /// <summary>
    /// Cancels all speech and clears queue.
    /// </summary>
    public void CancelAllSpeech()
    {
        _speechQueueService.CancelAll();
        _ttsService.StopPlayback();
    }

    public async Task SpeakAsync(string text, string? agentName = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogDebug("Skipping empty TTS text");
            return;
        }

        _logger.LogDebug("Speaking text: {Text}", TruncateText(text, 50));

        // Begin speaking - get cancellation token for this speech
        var speechToken = _speechQueueService.BeginSpeaking();

        // Link with external cancellation token
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, speechToken);

        try
        {
            // Use "assistant" as the voice source for all VirtualAssistant speech
            await _ttsService.SpeakAsync(text, source: "assistant", linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Speech cancelled: {Text}", TruncateText(text, 30));
        }
        finally
        {
            _speechQueueService.EndSpeaking();
        }
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        return text[..maxLength] + "...";
    }
}
