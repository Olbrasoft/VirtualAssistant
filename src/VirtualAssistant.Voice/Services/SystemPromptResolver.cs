using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;
using Olbrasoft.VirtualAssistant.Data.Queries.PromptQueries;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <inheritdoc />
public sealed class SystemPromptResolver : ISystemPromptResolver
{
    // Last-resort fallback when every DB priority level misses. The cache
    // key and ID are historical (baked into the seed migrations), so changes
    // here must match the default row in the prompts table.
    private const string FallbackPromptKey = "DefaultCorrection";
    private const int FallbackPromptId = 4;

    private readonly IPromptCache _promptCache;
    private readonly IDesktopContextService _desktopContextService;
    private readonly ICliAppDetector _cliAppDetector;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SystemPromptResolver> _logger;

    /// <summary>
    /// Registered as a Singleton, so we cannot capture the scoped
    /// <see cref="IQueryProcessor"/> (which transitively owns the EF Core
    /// DbContext) — that would be the captive-dependency bug that #972 already
    /// taught us about. Each <see cref="ResolveAsync"/> call instead creates
    /// a short-lived scope and pulls IQueryProcessor from it, matching the
    /// pattern established in <c>PromptResolver</c>.
    /// </summary>
    public SystemPromptResolver(
        IPromptCache promptCache,
        IDesktopContextService desktopContextService,
        ICliAppDetector cliAppDetector,
        IServiceScopeFactory scopeFactory,
        ILogger<SystemPromptResolver> logger)
    {
        _promptCache = promptCache ?? throw new ArgumentNullException(nameof(promptCache));
        _desktopContextService = desktopContextService ?? throw new ArgumentNullException(nameof(desktopContextService));
        _cliAppDetector = cliAppDetector ?? throw new ArgumentNullException(nameof(cliAppDetector));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<(string PromptText, int PromptId)> ResolveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var context = await _desktopContextService.GetCurrentContextAsync(cancellationToken);

            using var scope = _scopeFactory.CreateScope();
            var queryProcessor = scope.ServiceProvider.GetRequiredService<IQueryProcessor>();

            if (await TryResolveCliAppPromptAsync(queryProcessor, cancellationToken) is { } cliPrompt)
            {
                return cliPrompt;
            }

            _logger.LogDebug("Active window: '{Title}', app: '{App}', looking for matching prompt pattern",
                context.ActiveWindowTitle, context.ActiveApplication);

            var prompt = await queryProcessor.ProcessAsync(
                new GetPromptByAppIdPatternQuery(context.ActiveWindowTitle, context.ActiveApplication),
                cancellationToken);

            prompt ??= await queryProcessor.ProcessAsync(new GetDefaultPromptQuery(), cancellationToken);

            if (prompt == null)
            {
                _logger.LogError("CRITICAL: No default prompt found in database — using hardcoded fallback");
                return (_promptCache.GetPrompt(FallbackPromptKey), FallbackPromptId);
            }

            var promptText = _promptCache.GetPrompt(prompt.PromptFileName);
            _logger.LogDebug("Using prompt '{Prompt}' (ID: {Id}) for window '{Title}'",
                prompt.PromptFileName, prompt.Id, context.ActiveWindowTitle);

            return (promptText, prompt.Id);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Propagate user cancellation — a cancelled caller must not silently
            // get the fallback prompt (matches PromptResolver's behavior).
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving context-aware prompt, falling back to {Fallback} (ID {Id})",
                FallbackPromptKey, FallbackPromptId);
            return (_promptCache.GetPrompt(FallbackPromptKey), FallbackPromptId);
        }
    }

    private async Task<(string PromptText, int PromptId)?> TryResolveCliAppPromptAsync(
        IQueryProcessor queryProcessor,
        CancellationToken cancellationToken)
    {
        var cliApp = await _cliAppDetector.DetectCliAppAsync(cancellationToken);
        if (cliApp == null)
        {
            return null;
        }

        _logger.LogDebug("CLI app detected: {AppName} → using prompt '{Prompt}'",
            cliApp.AppName, cliApp.PromptFileName);

        var cliPrompt = await queryProcessor.ProcessAsync(
            new GetPromptByFileNameQuery(cliApp.PromptFileName),
            cancellationToken);

        if (cliPrompt == null)
        {
            _logger.LogWarning("CLI app '{App}' detected but prompt '{Prompt}' not found in database",
                cliApp.AppName, cliApp.PromptFileName);
            return null;
        }

        var cliPromptText = _promptCache.GetPrompt(cliPrompt.PromptFileName);
        _logger.LogDebug("Using prompt '{Prompt}' (ID: {Id}) for CLI app '{App}'",
            cliPrompt.PromptFileName, cliPrompt.Id, cliApp.AppName);
        return (cliPromptText, cliPrompt.Id);
    }
}
