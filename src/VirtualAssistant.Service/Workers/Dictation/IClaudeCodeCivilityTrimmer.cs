namespace Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

/// <summary>
/// Applies <c>CivilityTrimmer</c> to dictated text, but only when the active
/// CLI agent is Claude Code — per the feature request, "Děkuji." is a
/// legitimate message in chat apps and must pass through untouched. Lifts
/// the <c>ICliAppDetector</c> dependency out of <c>DictationWorker</c> so
/// detection errors are contained here (they fall back to returning the
/// text unchanged, because one stray civility word is cheaper than
/// mangling valid input when gdbus hiccups).
/// </summary>
public interface IClaudeCodeCivilityTrimmer
{
    /// <summary>
    /// Returns the input text trimmed of trailing civility if the currently-
    /// focused CLI app is Claude Code; otherwise returns <paramref name="text"/>
    /// unchanged. Propagates <see cref="OperationCanceledException"/>; swallows
    /// other detection errors as "no trim".
    /// </summary>
    Task<string> TrimIfClaudeCodeAsync(string text, CancellationToken cancellationToken);
}
