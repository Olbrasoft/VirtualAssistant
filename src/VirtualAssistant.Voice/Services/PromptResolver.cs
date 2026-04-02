using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;
using Olbrasoft.VirtualAssistant.Data.Queries.PromptQueries;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Resolves context-aware system prompt for LLM correction.
/// Uses IServiceScopeFactory to create its own DbContext scope, making it safe
/// for concurrent use in the racing pattern.
/// </summary>
public class PromptResolver : IPromptResolver
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPromptCache _promptCache;
    private readonly IDesktopContextService _desktopContextService;
    private readonly ICliAppDetector _cliAppDetector;
    private readonly ILogger<PromptResolver> _logger;

    public PromptResolver(
        IServiceScopeFactory scopeFactory,
        IPromptCache promptCache,
        IDesktopContextService desktopContextService,
        ICliAppDetector cliAppDetector,
        ILogger<PromptResolver> logger)
    {
        _scopeFactory = scopeFactory;
        _promptCache = promptCache;
        _desktopContextService = desktopContextService;
        _cliAppDetector = cliAppDetector;
        _logger = logger;
    }

    public async Task<(string PromptText, int PromptId)> ResolvePromptAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var queryProcessor = scope.ServiceProvider.GetRequiredService<IQueryProcessor>();

            var context = await _desktopContextService.GetCurrentContextAsync(ct);

            // Priority 1: Check for CLI apps running in terminals (e.g., Claude Code, OpenCode)
            var cliApp = await _cliAppDetector.DetectCliAppAsync(ct);
            if (cliApp != null)
            {
                _logger.LogDebug("CLI app detected: {AppName} -> using prompt '{Prompt}'",
                    cliApp.AppName, cliApp.PromptFileName);

                var cliPrompt = await queryProcessor.ProcessAsync(
                    new GetPromptByFileNameQuery(cliApp.PromptFileName), ct);

                if (cliPrompt != null)
                {
                    var cliPromptText = _promptCache.GetPrompt(cliPrompt.PromptFileName);
                    _logger.LogDebug("Using prompt '{Prompt}' (ID: {Id}) for CLI app '{App}'",
                        cliPrompt.PromptFileName, cliPrompt.Id, cliApp.AppName);
                    return (cliPromptText, cliPrompt.Id);
                }

                _logger.LogWarning("CLI app '{App}' detected but prompt '{Prompt}' not found in database",
                    cliApp.AppName, cliApp.PromptFileName);
            }

            // Priority 2: Match by window title or application pattern
            _logger.LogDebug("Active window: '{Title}', app: '{App}', looking for matching prompt pattern",
                context.ActiveWindowTitle, context.ActiveApplication);

            var prompt = await queryProcessor.ProcessAsync(
                new GetPromptByAppIdPatternQuery(context.ActiveWindowTitle, context.ActiveApplication), ct);

            // Priority 3: Default prompt
            prompt ??= await queryProcessor.ProcessAsync(new GetDefaultPromptQuery(), ct);

            if (prompt == null)
            {
                _logger.LogError("CRITICAL: No default prompt found in database - using hardcoded fallback");
                return (_promptCache.GetPrompt("DefaultCorrection"), 4);
            }

            var promptText = _promptCache.GetPrompt(prompt.PromptFileName);

            _logger.LogDebug("Using prompt '{Prompt}' (ID: {Id}) for window '{Title}'",
                prompt.PromptFileName, prompt.Id, context.ActiveWindowTitle);

            return (promptText, prompt.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving context-aware prompt, falling back to DefaultCorrection (ID 4)");
            return (_promptCache.GetPrompt("DefaultCorrection"), 4);
        }
    }
}
