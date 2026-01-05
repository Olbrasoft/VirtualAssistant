using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Queries.PromptQueries;

/// <summary>
/// Query to find a Prompt by matching app_id_pattern against active application.
/// Returns NULL if no match found (use GetDefaultPromptQuery as fallback).
/// </summary>
/// <param name="ActiveApplication">Active desktop application name (e.g., "code", "ferdium", "chrome").</param>
public record GetPromptByAppIdPatternQuery(string ActiveApplication) : IQuery<Prompt?>;
