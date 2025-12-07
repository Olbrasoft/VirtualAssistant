using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Voice.Services;
using VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Services;

/// <summary>
/// Single entry point for all TTS operations in VirtualAssistant.
/// This is the ONLY class that injects TtsService - all other components use IVirtualAssistantSpeaker.
/// </summary>
public class VirtualAssistantSpeaker : IVirtualAssistantSpeaker
{
    private readonly TtsService _ttsService;
    private readonly ILogger<VirtualAssistantSpeaker> _logger;

    public VirtualAssistantSpeaker(
        TtsService ttsService,
        ILogger<VirtualAssistantSpeaker> logger)
    {
        _ttsService = ttsService;
        _logger = logger;
    }

    public async Task SpeakAsync(string text, string? agentName = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogDebug("Skipping empty TTS text");
            return;
        }

        _logger.LogDebug("Speaking text: {Text}", TruncateText(text, 50));

        // Use "assistant" as the voice source for all VirtualAssistant speech
        await _ttsService.SpeakAsync(text, source: "assistant", ct);
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        return text[..maxLength] + "...";
    }
}
