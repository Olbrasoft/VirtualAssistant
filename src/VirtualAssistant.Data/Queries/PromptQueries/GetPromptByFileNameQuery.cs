using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Queries.PromptQueries;

/// <summary>
/// Query to get a prompt by its file name (e.g., "ClaudeCodeCorrection").
/// Used when CLI app detection identifies the prompt file to use.
/// </summary>
public record GetPromptByFileNameQuery(string PromptFileName) : IQuery<Prompt>;
