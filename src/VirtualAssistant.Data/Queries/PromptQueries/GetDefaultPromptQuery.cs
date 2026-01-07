using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Queries.PromptQueries;

/// <summary>
/// Query to get the Default prompt (AppIdPattern = "*").
/// This prompt is used as fallback when no specific prompt matches the active application.
/// </summary>
public record GetDefaultPromptQuery() : IQuery<Prompt>;
