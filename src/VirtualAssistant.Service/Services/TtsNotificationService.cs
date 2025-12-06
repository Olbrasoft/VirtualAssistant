using Olbrasoft.VirtualAssistant.Voice.Services;
using VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Services;

/// <summary>
/// Implementation of ITtsNotificationService that wraps TtsService.
/// This breaks the circular dependency between Core and Voice projects.
/// </summary>
public class TtsNotificationService : ITtsNotificationService
{
    private readonly TtsService _ttsService;

    public TtsNotificationService(TtsService ttsService)
    {
        _ttsService = ttsService;
    }

    public Task SpeakAsync(string text, string? source = null, CancellationToken ct = default)
    {
        return _ttsService.SpeakAsync(text, source, ct);
    }
}
