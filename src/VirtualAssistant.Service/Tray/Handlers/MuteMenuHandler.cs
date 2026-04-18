using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Tray.Handlers;

/// <inheritdoc />
public sealed class MuteMenuHandler : IMuteMenuHandler
{
    private readonly ILogger<MuteMenuHandler> _logger;
    private readonly IManualMuteService _muteService;
    private readonly ISettingsService _settingsService;

    public MuteMenuHandler(
        ILogger<MuteMenuHandler> logger,
        IManualMuteService muteService,
        ISettingsService settingsService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _muteService = muteService ?? throw new ArgumentNullException(nameof(muteService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    }

    public void HandleMuteToggle()
    {
        try
        {
            _muteService.Toggle();
            _logger.LogInformation("Mute toggled via tray menu to: {IsMuted}", _muteService.IsMuted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle mute from tray menu");
        }
    }

    public async Task HandleTtsMuteToggleAsync(bool muted)
    {
        try
        {
            await _settingsService.SetAsync("tts.muted", muted);
            _logger.LogInformation("TTS mute set via tray menu to: {IsMuted}", muted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set TTS mute from tray menu");
        }
    }
}
