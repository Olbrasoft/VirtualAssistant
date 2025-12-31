using System.Diagnostics;
using Microsoft.Extensions.Logging;
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
    public async Task HandleTtsMuteToggleAsync()
    {
        try
        {
            var currentState = await _settingsService.GetAsync("tts.muted", false);
            var newState = !currentState;
            await _settingsService.SetAsync("tts.muted", newState);

            _logger.LogInformation("TTS mute toggled via tray menu to: {IsMuted}", newState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle TTS mute from tray menu");
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
            _logger.LogInformation("Reloading LLM prompt...");

            // Copy prompt from source to deployment location
            var sourceFile = "/home/jirka/Olbrasoft/VirtualAssistant/src/VirtualAssistant.Voice/Prompts/MistralSystemPrompt.md";
            var deployDir = "/opt/olbrasoft/virtual-assistant/app/Prompts";
            var deployFile = Path.Combine(deployDir, "MistralSystemPrompt.md");

            // Create deployment directory if it doesn't exist
            if (!Directory.Exists(deployDir))
            {
                Directory.CreateDirectory(deployDir);
                _logger.LogInformation("Created deployment directory: {Directory}", deployDir);
            }

            // Copy file from source to deployment location
            if (File.Exists(sourceFile))
            {
                File.Copy(sourceFile, deployFile, overwrite: true);
                _logger.LogInformation("Copied prompt from {Source} to {Destination}", sourceFile, deployFile);
            }
            else
            {
                _logger.LogWarning("Source prompt file not found: {SourceFile}", sourceFile);
            }

            // Clear cache and reload prompt
            _llmProvider.ReloadPrompt();
            _logger.LogInformation("LLM prompt reloaded successfully");
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
