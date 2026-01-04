using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtualAssistant.Core.Models;
using VirtualAssistant.Core.Services;
using VirtualAssistant.Desktop.Configuration;

namespace VirtualAssistant.Desktop.Services;

/// <summary>
/// Selects appropriate system prompt based on desktop context.
/// </summary>
public class ContextPromptSelector : IContextPromptSelector
{
    private readonly ContextMappingOptions _options;
    private readonly ILogger<ContextPromptSelector> _logger;

    public ContextPromptSelector(
        IOptions<ContextMappingOptions> options,
        ILogger<ContextPromptSelector> logger)
    {
        _options = options.Value;
        _logger = logger;
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

        if (_options.Programming.Any(app => appLower.Contains(app.ToLowerInvariant())))
            return ContextType.Programming;

        if (_options.Chat.Any(app => appLower.Contains(app.ToLowerInvariant())))
            return ContextType.Chat;

        if (_options.Browsing.Any(app => appLower.Contains(app.ToLowerInvariant())))
            return ContextType.Browsing;

        return ContextType.General;
    }
}
