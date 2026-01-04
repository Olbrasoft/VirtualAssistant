using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Desktop.Configuration;

namespace Olbrasoft.VirtualAssistant.Desktop.Services;

/// <summary>
/// Selects appropriate system prompt based on desktop context.
/// </summary>
public class ContextPromptSelector : IContextPromptSelector
{
    private readonly ContextMappingOptions _options;
    private readonly ILogger<ContextPromptSelector> _logger;

    // Cached lowercase versions for performance
    private readonly string[] _programmingLower;
    private readonly string[] _chatLower;
    private readonly string[] _browsingLower;

    public ContextPromptSelector(
        IOptions<ContextMappingOptions> options,
        ILogger<ContextPromptSelector> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Cache lowercase versions to avoid repeated allocations in hot path
        _programmingLower = _options.Programming.Select(s => s.ToLowerInvariant()).ToArray();
        _chatLower = _options.Chat.Select(s => s.ToLowerInvariant()).ToArray();
        _browsingLower = _options.Browsing.Select(s => s.ToLowerInvariant()).ToArray();
    }

    public Task<string> SelectPromptAsync(DesktopContext? context, CancellationToken ct = default)
    {
        if (context == null)
        {
            _logger.LogWarning("Desktop context unavailable, using general prompt");
            return Task.FromResult("general.txt");
        }

        var contextType = DetectContextType(context.ActiveApplication);
        var promptFile = contextType switch
        {
            ContextType.Programming => "programming.txt",
            ContextType.Chat => "chat.txt",
            ContextType.Browsing => "search.txt",
            _ => "general.txt"
        };

        _logger.LogInformation(
            "Selected {PromptFile} for app {App} (context type: {Type})",
            promptFile,
            context.ActiveApplication,
            contextType
        );

        return Task.FromResult(promptFile);
    }

    public ContextType DetectContextType(string applicationId)
    {
        var appLower = applicationId.ToLowerInvariant();

        // Use cached lowercase arrays for performance
        if (_programmingLower.Any(app => appLower.Contains(app)))
            return ContextType.Programming;

        if (_chatLower.Any(app => appLower.Contains(app)))
            return ContextType.Chat;

        if (_browsingLower.Any(app => appLower.Contains(app)))
            return ContextType.Browsing;

        return ContextType.General;
    }
}
