using Microsoft.Extensions.DependencyInjection;
using Olbrasoft.VirtualAssistant.Core.Audio;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Desktop.Services;
using Olbrasoft.VirtualAssistant.Service.Workers;
using Olbrasoft.VirtualAssistant.Voice.StateMachine;

namespace Olbrasoft.VirtualAssistant.Service.Infrastructure;

/// <summary>
/// Handles state synchronization between services using Observer pattern.
/// Subscribes to mute and dictation state changes, updates UI and menu accordingly.
/// Implements Single Responsibility Principle - only handles state notifications.
/// </summary>
public class StateNotificationHandler : IStateNotificationHandler
{
    private readonly ILogger<StateNotificationHandler> _logger;
    private readonly IManualMuteService _muteService;
    private readonly ISettingsService _settingsService;
    private readonly IServiceStatusUpdater _statusUpdater;
    private readonly ITrayIconCoordinator _iconCoordinator;
    private readonly IIconAnimationService _iconAnimationService;
    private readonly IServiceLifecycleManager? _lifecycleManager;
    private readonly IDictationStateMachine? _dictationStateMachine;
    private readonly DictationWorker? _dictationWorker;
    private readonly IRecordingNotificationService? _recordingNotificationService;
    private readonly IRecordingOverlayService? _recordingOverlayService;
    private readonly ISoundEffectPlayer? _recordingStartSoundPlayer;

    public StateNotificationHandler(
        ILogger<StateNotificationHandler> logger,
        IManualMuteService muteService,
        ISettingsService settingsService,
        IServiceStatusUpdater statusUpdater,
        ITrayIconCoordinator iconCoordinator,
        IIconAnimationService iconAnimationService,
        IServiceLifecycleManager? lifecycleManager = null,
        IDictationStateMachine? dictationStateMachine = null,
        DictationWorker? dictationWorker = null,
        IRecordingNotificationService? recordingNotificationService = null,
        IRecordingOverlayService? recordingOverlayService = null,
        [FromKeyedServices("recording-start")] ISoundEffectPlayer? recordingStartSoundPlayer = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _muteService = muteService ?? throw new ArgumentNullException(nameof(muteService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _statusUpdater = statusUpdater ?? throw new ArgumentNullException(nameof(statusUpdater));
        _iconCoordinator = iconCoordinator ?? throw new ArgumentNullException(nameof(iconCoordinator));
        _iconAnimationService = iconAnimationService ?? throw new ArgumentNullException(nameof(iconAnimationService));
        _lifecycleManager = lifecycleManager;
        _dictationStateMachine = dictationStateMachine;
        _dictationWorker = dictationWorker;
        _recordingNotificationService = recordingNotificationService;
        _recordingOverlayService = recordingOverlayService;
        _recordingStartSoundPlayer = recordingStartSoundPlayer;
    }

    /// <summary>
    /// Subscribes to state change events (mute, dictation).
    /// </summary>
    public void SubscribeToEvents()
    {
        // Subscribe to mute state changes
        _muteService.MuteStateChanged += OnMuteStateChanged;

        // Subscribe to dictation state changes
        if (_dictationStateMachine != null)
        {
            _dictationStateMachine.StateChanged += OnDictationStateChanged;
        }

        _logger.LogDebug("State notification handler subscribed to events");
    }

    /// <summary>
    /// Unsubscribes from state change events.
    /// </summary>
    public void UnsubscribeFromEvents()
    {
        // Unsubscribe from mute service
        _muteService.MuteStateChanged -= OnMuteStateChanged;

        // Unsubscribe from dictation state machine
        if (_dictationStateMachine != null)
        {
            _dictationStateMachine.StateChanged -= OnDictationStateChanged;
        }

        _logger.LogDebug("State notification handler unsubscribed from events");
    }

    /// <summary>
    /// Initializes states on startup (mute, dictation, TTS mute, service status).
    /// </summary>
    public async Task InitializeStatesAsync()
    {
        try
        {
            // Update menu handler with initial mute state
            _statusUpdater.UpdateMuteState(_muteService.IsMuted);

            // Initialize dictation state (sync menu with DictationWorker default state)
            // DictationWorker manages its own state - we only synchronize the menu
            _statusUpdater.UpdateDictationStatus(true);
            _logger.LogDebug("Dictation menu status initialized (synced with worker default)");

            // Initialize TTS mute state
            var ttsMuted = await _settingsService.GetAsync("tts.muted", false);
            _statusUpdater.UpdateTtsMuteState(ttsMuted);
            _logger.LogInformation("TTS mute state initialized: {IsMuted}", ttsMuted);

            // Refresh service statuses
            // NOTE: RefreshSpeechToTextStatusAsync removed (issue #466) - STT runs inline now
            if (_lifecycleManager != null)
            {
                await _lifecycleManager.RefreshLogViewerStatusAsync();
            }

            _logger.LogDebug("State notification handler states initialized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize states");
            throw;
        }
    }

    /// <summary>
    /// Handles mute state changes - updates tray icon and menu.
    /// </summary>
    private void OnMuteStateChanged(object? sender, bool isMuted)
    {
        try
        {
            // Update tray icon
            _iconCoordinator.UpdateCenterIcon(isMuted);

            // Update menu handler mute state
            _statusUpdater.UpdateMuteState(isMuted);

            _logger.LogDebug("Mute state updated: {IsMuted}", isMuted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update mute state");
        }
    }

    /// <summary>
    /// Handles dictation state changes - delegates to icon animation service and shows overlay/notifications.
    /// </summary>
    private async void OnDictationStateChanged(object? sender, DictationState newState)
    {
        try
        {
            _logger.LogInformation("Dictation state changed to: {State}", newState);

            // Play recording start sound when entering Recording state
            if (newState == DictationState.Recording)
            {
                _recordingStartSoundPlayer?.Play();
            }

            // Delegate icon animation to specialized service
            _iconAnimationService.HandleDictationStateChange(newState);

            // Show recording/transcribing overlay or notifications
            // Prefer overlay service (Phase 2 - issue #672), fallback to notifications (Phase 1 - issue #670)
            await HandleRecordingFeedbackAsync(newState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle dictation state change: {State}", newState);
        }
    }

    /// <summary>
    /// Handles visual feedback for recording state changes.
    /// DISABLED: AT-SPI caret tracking causes UI lag on Wayland, and overlay positioning
    /// without caret tracking is not useful. Tray icon animation is sufficient feedback.
    /// </summary>
    private Task HandleRecordingFeedbackAsync(DictationState newState)
    {
        // Recording overlay/notification disabled - tray icon animation provides sufficient feedback
        _logger.LogDebug("Recording feedback disabled, state: {State}", newState);
        return Task.CompletedTask;
    }
}
