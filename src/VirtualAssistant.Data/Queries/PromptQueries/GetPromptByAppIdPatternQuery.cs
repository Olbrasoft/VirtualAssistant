using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Queries.PromptQueries;

/// <summary>
/// Query to find a Prompt by matching app_id_pattern against active window title.
/// Tests pattern against window title (e.g., "Claude Code", "Ferdium - WhatsApp", "OpenCode").
/// Returns NULL if no match found (use GetDefaultPromptQuery as fallback).
/// </summary>
/// <param name="ActiveWindowTitle">Active window title from desktop context.</param>
public record GetPromptByAppIdPatternQuery(string ActiveWindowTitle) : IQuery<Prompt?>;
