using System.Diagnostics;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Service.Tray;

/// <summary>
/// Dispatches menu events from tray icon context menu to appropriate handlers.
/// Implements Command pattern for menu actions.
/// </summary>
public class MenuEventDispatcher : IMenuEventDispatcher
{
    private readonly ILogger<MenuEventDispatcher> _logger;
    private readonly IManualMuteService _muteService;
    private readonly ISettingsService _settingsService;
    private readonly int _logViewerPort;
    private readonly ILlmProvider? _llmProvider;
    private readonly IDictationControl? _dictationControl;

    public MenuEventDispatcher(
        ILogger<MenuEventDispatcher> logger,
        IManualMuteService muteService,
        ISettingsService settingsService,
        int logViewerPort,
        ILlmProvider? llmProvider = null,
        IDictationControl? dictationControl = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _muteService = muteService ?? throw new ArgumentNullException(nameof(muteService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logViewerPort = logViewerPort;
        _llmProvider = llmProvider;
        _dictationControl = dictationControl;
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public void HandleShowLogs()
    {
        try
        {
            var logsUrl = $"http://localhost:{_logViewerPort}";
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = logsUrl,
                    UseShellExecute = true
                }
            };
            process.Start();
            _logger.LogInformation("Opened logs viewer at {Url}", logsUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open logs viewer");
        }
    }

    /// <inheritdoc/>
    public void HandleLlmCorrectionToggle(bool enabled)
    {
        if (_llmProvider == null)
        {
            _logger.LogWarning("LLM provider not available");
            return;
        }

        try
        {
            _logger.LogInformation("Toggling LLM correction to: {Enabled}", enabled);
            _llmProvider.SetEnabled(enabled);
            _logger.LogInformation("LLM correction successfully {Status}", enabled ? "enabled" : "disabled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle LLM correction");
        }
    }

    /// <inheritdoc/>
    public void HandleReloadPrompt()
    {
        if (_llmProvider == null)
        {
            _logger.LogWarning("LLM provider not available");
            return;
        }

        try
        {
            _logger.LogInformation("Reloading LLM prompts...");

            // Prompts are deployed via deploy.sh script, no manual copy needed
            // Just clear the cache and reload prompts from deployment location
            _llmProvider.ReloadPrompt();
            _logger.LogInformation("LLM prompts reloaded successfully from deployment location");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload LLM prompt");
        }
    }

    /// <inheritdoc/>
    public void HandleDictationToggle(bool enabled)
    {
        try
        {
            if (_dictationControl == null)
            {
                _logger.LogWarning("Dictation control not available");
                return;
            }

            _logger.LogInformation("Setting dictation enabled: {Enabled}", enabled);
            _dictationControl.SetDictationEnabled(enabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle dictation");
        }
    }
}
