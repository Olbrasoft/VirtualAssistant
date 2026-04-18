using Microsoft.AspNetCore.SignalR;
using Olbrasoft.VirtualAssistant.Core.Audio;
using Olbrasoft.VirtualAssistant.Core.Keyboard;
using Olbrasoft.VirtualAssistant.Service.Hubs;

namespace Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

/// <inheritdoc />
public sealed class DictationOutputChannel : IDictationOutputChannel
{
    private readonly ILogger<DictationOutputChannel> _logger;
    private readonly IKeyboardSimulationService _keyboardSimulation;
    private readonly ISoundEffectPlayer _typingSound;
    private readonly ISoundEffectPlayer _cancelSound;
    private readonly IHubContext<DictationHub> _hubContext;

    public DictationOutputChannel(
        ILogger<DictationOutputChannel> logger,
        IKeyboardSimulationService keyboardSimulation,
        ISoundEffectPlayer typingSound,
        ISoundEffectPlayer cancelSound,
        IHubContext<DictationHub> hubContext)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _keyboardSimulation = keyboardSimulation ?? throw new ArgumentNullException(nameof(keyboardSimulation));
        _typingSound = typingSound ?? throw new ArgumentNullException(nameof(typingSound));
        _cancelSound = cancelSound ?? throw new ArgumentNullException(nameof(cancelSound));
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
    }

    public void StartTypingFeedback() => _typingSound.StartLoop();

    public void StopTypingFeedback() => _typingSound.StopLoop();

    public void PlayCancelCue() => _cancelSound.Play();

    public Task<bool> FastPasteAsync(string text, CancellationToken cancellationToken) =>
        _keyboardSimulation.FastPasteAsync(text, cancellationToken);

    public Task<bool> TypeIntoActiveWindowAsync(string text, CancellationToken cancellationToken) =>
        _keyboardSimulation.TypeIntoActiveWindowAsync(text, cancellationToken);

    public async Task BroadcastEventAsync(DictationEventType eventType, string? text, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync(
                "DictationEvent",
                new DictationEvent { EventType = eventType, Text = text },
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Broadcasting is best-effort — a SignalR client-side failure must
            // not take down the dictation pipeline itself.
            _logger.LogDebug(ex, "Failed to broadcast dictation event to SignalR clients");
        }
    }
}
