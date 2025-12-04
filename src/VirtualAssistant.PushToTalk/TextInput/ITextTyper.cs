namespace Olbrasoft.VirtualAssistant.PushToTalk.TextInput;

/// <summary>
/// Interface for simulating keyboard text input.
/// </summary>
public interface ITextTyper
{
    /// <summary>
    /// Types the specified text by simulating keyboard input.
    /// </summary>
    /// <param name="text">Text to type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task TypeTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value indicating whether the typer is available on the current platform.
    /// </summary>
    bool IsAvailable { get; }
}
