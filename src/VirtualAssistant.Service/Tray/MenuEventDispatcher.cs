using System.Diagnostics;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Services;
using Olbrasoft.VirtualAssistant.Service.Tray.Menu;
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
    private readonly IPromptSyncService? _promptSyncService;
    private readonly IMenuStateManager? _menuStateManager;

    public MenuEventDispatcher(
        ILogger<MenuEventDispatcher> logger,
        IManualMuteService muteService,
        ISettingsService settingsService,
        int logViewerPort,
        ILlmProvider? llmProvider = null,
        IDictationControl? dictationControl = null,
        IPromptSyncService? promptSyncService = null,
        IMenuStateManager? menuStateManager = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _muteService = muteService ?? throw new ArgumentNullException(nameof(muteService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logViewerPort = logViewerPort;
        _llmProvider = llmProvider;
        _dictationControl = dictationControl;
        _promptSyncService = promptSyncService;
        _menuStateManager = menuStateManager;
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
            _logger.LogInformation("Syncing LLM prompts...");

            // Update menu to show syncing state
            _menuStateManager?.UpdatePromptSyncStatus(PromptSyncStatus.Syncing);

            // Sync prompts from source to deployment directory
            if (_promptSyncService != null)
            {
                var result = _promptSyncService.SyncPrompts();

                if (result.Success)
                {
                    _logger.LogInformation("Prompt sync completed. Copied {Count} files.", result.FilesCopied);

                    // Clear cache so prompts are reloaded from newly copied files
                    _llmProvider.ReloadPrompt();

                    _menuStateManager?.UpdatePromptSyncStatus(PromptSyncStatus.InSync);
                    _logger.LogInformation("LLM prompts synced and reloaded successfully");
                }
                else
                {
                    var errorMsg = string.Join("; ", result.Errors);
                    _logger.LogError("Prompt sync failed: {Errors}", errorMsg);
                    _menuStateManager?.UpdatePromptSyncStatus(PromptSyncStatus.SyncFailed, errorMsg);
                }
            }
            else
            {
                // Fallback: just clear cache (old behavior)
                _logger.LogWarning("PromptSyncService not available - ensure PromptSync:SourcePath is configured in appsettings.json. Only clearing LLM prompt cache.");
                _llmProvider.ReloadPrompt();
                _menuStateManager?.UpdatePromptSyncStatus(PromptSyncStatus.Unknown);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync LLM prompts");
            _menuStateManager?.UpdatePromptSyncStatus(PromptSyncStatus.SyncFailed, ex.Message);
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
