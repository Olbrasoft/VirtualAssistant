using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Queries.PromptQueries;

/// <summary>
/// Query to find a Prompt by matching against active window title or application name.
/// Priority: 1) ApplicationPattern matches ActiveApplication (e.g., "antigravity.desktop")
///           2) AppIdPattern matches ActiveWindowTitle (e.g., "Claude Code", "OC |")
/// Returns NULL if no match found (use GetDefaultPromptQuery as fallback).
/// </summary>
/// <param name="ActiveWindowTitle">Active window title from desktop context.</param>
/// <param name="ActiveApplication">Active application desktop file name (e.g., "antigravity.desktop").</param>
public record GetPromptByAppIdPatternQuery(string ActiveWindowTitle, string? ActiveApplication = null) : IQuery<Prompt?>;
