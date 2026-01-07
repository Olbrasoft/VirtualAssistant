namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Parses JSON output from Claude Code headless mode.
/// </summary>
public interface IClaudeOutputParser
{
    /// <summary>
    /// Parse Claude JSON output and return execution result.
    /// </summary>
    /// <param name="output">Raw JSON output from Claude process</param>
    /// <returns>Parsed execution result</returns>
    ClaudeExecutionResult Parse(string output);
}
