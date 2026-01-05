using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.StateMachine;

namespace Olbrasoft.VirtualAssistant.Service.Infrastructure;

/// <summary>
/// Manages hand icon animations based on dictation state.
/// Implements Single Responsibility Principle - only handles icon animations.
/// </summary>
public class IconAnimationService : IIconAnimationService
{
    private readonly ITrayIconCoordinator _iconCoordinator;
    private readonly ILogger<IconAnimationService> _logger;

    public IconAnimationService(
        ITrayIconCoordinator iconCoordinator,
        ILogger<IconAnimationService> logger)
    {
        _iconCoordinator = iconCoordinator ?? throw new ArgumentNullException(nameof(iconCoordinator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Updates hand icons based on dictation state change.
    /// </summary>
    /// <param name="newState">New dictation state</param>
    public void HandleDictationStateChange(DictationState newState)
    {
        try
        {
            _logger.LogInformation("Dictation state changed to: {State}", newState);

            switch (newState)
            {
                case DictationState.Idle:
                    // Return to default state
                    _iconCoordinator.SetRightHandIcon("default-right-hand.svg");
                    _iconCoordinator.SetCenterIcon("default-head.svg");
                    break;

                case DictationState.Recording:
                    // Show active dictation state
                    _iconCoordinator.SetRightHandIcon("holding-up-a-microphone-right-hand.svg");
                    _iconCoordinator.SetCenterIcon("listening-dictation-head.svg");
                    break;

                case DictationState.Transcribing:
                    // Show transcription in progress
                    _iconCoordinator.SetRightHandIcon("writing-right-hand.svg");
                    _iconCoordinator.SetCenterIcon("busy-head.svg");
                    break;

                default:
                    _logger.LogWarning("Unknown dictation state: {State}", newState);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update icon for dictation state: {State}", newState);
        }
    }
}
