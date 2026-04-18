using System.Diagnostics;
using Olbrasoft.VirtualAssistant.Service.Services;
using Olbrasoft.VirtualAssistant.Service.Tray.Menu;
using Olbrasoft.VirtualAssistant.Voice.Filters;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Service.Tray.Handlers;

/// <inheritdoc />
public sealed class LlmMenuHandler : ILlmMenuHandler
{
    private readonly ILogger<LlmMenuHandler> _logger;
    private readonly ILlmProvider? _llmProvider;
    private readonly IPromptSyncService? _promptSyncService;
    private readonly IMenuStateManager? _menuStateManager;
    private readonly DatabaseCorrectionFilterStrategy? _correctionFilter;

    public LlmMenuHandler(
        ILogger<LlmMenuHandler> logger,
        ILlmProvider? llmProvider = null,
        IPromptSyncService? promptSyncService = null,
        IMenuStateManager? menuStateManager = null,
        DatabaseCorrectionFilterStrategy? correctionFilter = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _llmProvider = llmProvider;
        _promptSyncService = promptSyncService;
        _menuStateManager = menuStateManager;
        _correctionFilter = correctionFilter;
    }

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
            _menuStateManager?.UpdatePromptSyncStatus(PromptSyncStatus.Syncing);

            if (_promptSyncService != null)
            {
                var result = _promptSyncService.SyncPrompts();

                if (result.Success)
                {
                    _logger.LogInformation("Prompt sync completed. Copied {Count} files.", result.FilesCopied);
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
                // Fallback: just clear cache (legacy behavior before PromptSync was wired up).
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

    public void HandleMercuryBilling()
    {
        try
        {
            const string billingUrl = "https://platform.inceptionlabs.ai/dashboard/billing";
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = billingUrl,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            process.Start();
            _logger.LogInformation("Opened Mercury billing dashboard at {Url}", billingUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open Mercury billing dashboard");
        }
    }

    /// <summary>
    /// Invalidates the transcription corrections cache so a freshly INSERTed row
    /// in <c>transcription_corrections</c> takes effect on the very next dictation.
    /// No-op if the filter strategy is not available (e.g. running in tests).
    /// </summary>
    public void HandleReloadCorrectionsCache()
    {
        if (_correctionFilter is null)
        {
            _logger.LogWarning("Transcription correction filter not available; cache invalidation skipped");
            return;
        }

        try
        {
            _correctionFilter.InvalidateCache();
            _logger.LogInformation("Transcription corrections cache invalidated from tray menu");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate transcription corrections cache");
        }
    }
}
