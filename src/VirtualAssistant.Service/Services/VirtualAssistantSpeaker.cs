using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Services;

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
    private readonly ISettingsService _settingsService;
    private readonly ILogger<VirtualAssistantSpeaker> _logger;

    public VirtualAssistantSpeaker(
        TtsService ttsService,
        ISpeechQueueService speechQueueService,
        ISettingsService settingsService,
        ILogger<VirtualAssistantSpeaker> logger)
    {
        _ttsService = ttsService;
        _speechQueueService = speechQueueService;
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <summary>
    /// Whether speech is currently playing (includes both generation and playback).
    /// </summary>
    public bool IsSpeaking => _speechQueueService.IsSpeaking;

    /// <summary>
    /// Whether audio is currently playing (excludes generation phase).
    /// Use this to check if user can actually hear the speech.
    /// </summary>
    public bool IsPlaying => _ttsService.IsPlaying;

    /// <summary>
    /// Number of messages waiting in TTS queue.
    /// </summary>
    public int QueueCount => _ttsService.QueueCount;

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

    /// <summary>
    /// Speaks the given text using the text-to-speech service.
    /// </summary>
    /// <param name="text">The text to speak. Empty or whitespace text is skipped.</param>
    /// <param name="agentName">Optional agent name for identification (not currently used).</param>
    /// <param name="skipCache">If true, bypasses TTS audio cache and regenerates speech.</param>
    /// <param name="ct">Cancellation token to stop speech playback.</param>
    /// <returns>Result of the TTS operation including provider used and duration.</returns>
    public async Task<TtsResult> SpeakAsync(string text, string? agentName = null, bool skipCache = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogDebug("Skipping empty TTS text");
            return new TtsResult(Success: false, Skipped: true);
        }

        // Check if TTS is muted
        var isMuted = await _settingsService.GetAsync("tts.muted", false);
        if (isMuted)
        {
            _logger.LogDebug("TTS is muted - skipping speech: {Text}", TruncateText(text, 50));
            return new TtsResult(Success: false, Skipped: true);
        }

        _logger.LogDebug("Speaking text: {Text} (skipCache: {SkipCache})", TruncateText(text, 50), skipCache);

        // Begin speaking - get cancellation token for this speech
        var speechToken = _speechQueueService.BeginSpeaking();

        // Link with external cancellation token
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, speechToken);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Use "assistant" as the voice source for all VirtualAssistant speech
            var (success, providerUsed) = await _ttsService.SpeakAsync(text, source: "assistant", skipCache, linkedCts.Token);
            stopwatch.Stop();

            return new TtsResult(
                Success: success,
                ProviderUsed: providerUsed,
                DurationMs: (int)stopwatch.ElapsedMilliseconds
            );
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogDebug("Speech cancelled: {Text}", TruncateText(text, 30));
            return new TtsResult(Success: false, Cancelled: true, DurationMs: (int)stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            _speechQueueService.EndSpeaking();
        }
    }

    /// <summary>
    /// Plays all queued messages immediately.
    /// Called when speech lock is released to flush pending messages.
    /// </summary>
    public async Task FlushQueueAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Flushing TTS queue ({Count} messages)", _ttsService.QueueCount);
        await _ttsService.FlushQueueAsync(ct);
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        return text[..maxLength] + "...";
    }
}
