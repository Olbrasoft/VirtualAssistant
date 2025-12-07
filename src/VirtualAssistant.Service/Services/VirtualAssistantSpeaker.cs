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
    private readonly IWorkspaceDetectionService _workspaceService;
    private readonly ILogger<VirtualAssistantSpeaker> _logger;

    public VirtualAssistantSpeaker(
        TtsService ttsService,
        IWorkspaceDetectionService workspaceService,
        ILogger<VirtualAssistantSpeaker> logger)
    {
        _ttsService = ttsService;
        _workspaceService = workspaceService;
        _logger = logger;
    }

    public async Task SpeakAsync(string text, string? agentName = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogDebug("Skipping empty TTS text");
            return;
        }

        // If agent name is provided, check workspace
        if (!string.IsNullOrEmpty(agentName))
        {
            var isOnSameWorkspace = await _workspaceService.IsUserOnAgentWorkspaceAsync(agentName, ct);

            if (isOnSameWorkspace)
            {
                _logger.LogInformation(
                    "Skipping TTS for agent {Agent} - user is on same workspace. Text: {Text}",
                    agentName, TruncateText(text, 50));
                return;
            }

            _logger.LogDebug(
                "Speaking for agent {Agent} - user is on different workspace. Text: {Text}",
                agentName, TruncateText(text, 50));
        }
        else
        {
            _logger.LogDebug("Speaking without agent context. Text: {Text}", TruncateText(text, 50));
        }

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
