using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Voice.Services;
using VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Services;

/// <summary>
/// Implementation of ITtsNotificationService that wraps TtsService.
/// Supports workspace-aware notifications to skip TTS when user is on same workspace as agent.
/// </summary>
public class TtsNotificationService : ITtsNotificationService
{
    private readonly TtsService _ttsService;
    private readonly IWorkspaceDetectionService _workspaceService;
    private readonly ILogger<TtsNotificationService> _logger;

    public TtsNotificationService(
        TtsService ttsService,
        IWorkspaceDetectionService workspaceService,
        ILogger<TtsNotificationService> logger)
    {
        _ttsService = ttsService;
        _workspaceService = workspaceService;
        _logger = logger;
    }

    public Task SpeakAsync(string text, string? source = null, CancellationToken ct = default)
    {
        return _ttsService.SpeakAsync(text, source, ct);
    }

    public async Task SpeakIfNotOnAgentWorkspaceAsync(
        string text,
        string agentName,
        string? source = null,
        CancellationToken ct = default)
    {
        // Check if user is on same workspace as agent
        var isOnSameWorkspace = await _workspaceService.IsUserOnAgentWorkspaceAsync(agentName, ct);

        if (isOnSameWorkspace)
        {
            _logger.LogDebug(
                "Skipping TTS notification for {Agent} - user is on same workspace",
                agentName);
            return;
        }

        await _ttsService.SpeakAsync(text, source, ct);
    }
}
