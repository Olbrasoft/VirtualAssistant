using Olbrasoft.VirtualAssistant.Core.StateMachine;

namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Manages hand icon animations based on dictation state.
/// Implements Single Responsibility Principle - only handles icon animations.
/// </summary>
public interface IIconAnimationService
{
    /// <summary>
    /// Updates hand icons based on dictation state change.
    /// </summary>
    /// <param name="newState">New dictation state</param>
    void HandleDictationStateChange(DictationState newState);
}
