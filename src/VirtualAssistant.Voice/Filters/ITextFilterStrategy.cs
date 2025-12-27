namespace Olbrasoft.VirtualAssistant.Voice.Filters;

/// <summary>
/// Defines a single text filtering operation.
/// Follows Strategy pattern for composable text transformations.
/// </summary>
public interface ITextFilterStrategy
{
    /// <summary>
    /// Gets the human-readable name of this filter strategy.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets whether this filter is currently enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Applies this filter strategy to the input text.
    /// </summary>
    /// <param name="text">Text to filter.</param>
    /// <returns>Filtered text after applying this strategy.</returns>
    string Apply(string text);
}
