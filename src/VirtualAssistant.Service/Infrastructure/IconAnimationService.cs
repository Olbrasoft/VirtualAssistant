using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.StateMachine;

namespace Olbrasoft.VirtualAssistant.Service.Infrastructure;

public class IconAnimationService : IIconAnimationService
{
    private readonly ITrayIconCoordinator _iconCoordinator;
    private readonly ILogger<IconAnimationService> _logger;
    private readonly Dictionary<DictationState, (string RightHand, string Center)> _stateToIconMap;

    public IconAnimationService(
        ITrayIconCoordinator iconCoordinator,
        ILogger<IconAnimationService> logger)
    {
        _iconCoordinator = iconCoordinator ?? throw new ArgumentNullException(nameof(iconCoordinator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stateToIconMap = CreateStateToIconMap();
    }

    public void HandleDictationStateChange(DictationState newState)
    {
        try
        {
            _logger.LogInformation("Dictation state changed to: {State}", newState);

            if (_stateToIconMap.TryGetValue(newState, out var icons))
            {
                _iconCoordinator.SetRightHandIcon(icons.RightHand);
                _iconCoordinator.SetCenterIcon(icons.Center);
            }
            else
            {
                _logger.LogWarning("Unknown dictation state: {State}", newState);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update icon for dictation state: {State}", newState);
        }
    }

    private static Dictionary<DictationState, (string RightHand, string Center)> CreateStateToIconMap() => new()
    {
        [DictationState.Idle] = ("default-right-hand.svg", "default-head.svg"),
        [DictationState.Recording] = ("holding-up-a-microphone-right-hand.svg", "listening-dictation-head.svg"),
        [DictationState.Transcribing] = ("writing-right-hand.svg", "busy-head.svg")
    };
}
