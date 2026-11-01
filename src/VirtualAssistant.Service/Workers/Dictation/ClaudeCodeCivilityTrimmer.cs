using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;

namespace Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

/// <inheritdoc />
public sealed class ClaudeCodeCivilityTrimmer : IClaudeCodeCivilityTrimmer
{
    private readonly ILogger<ClaudeCodeCivilityTrimmer> _logger;
    private readonly ICliAppDetector _cliAppDetector;

    public ClaudeCodeCivilityTrimmer(
        ILogger<ClaudeCodeCivilityTrimmer> logger,
        ICliAppDetector cliAppDetector)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cliAppDetector = cliAppDetector ?? throw new ArgumentNullException(nameof(cliAppDetector));
    }

    public async Task<string> TrimIfClaudeCodeAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        CliAppDetectionResult? cliApp;
        try
        {
            cliApp = await _cliAppDetector.DetectCliAppAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "CLI app detection failed before quick paste — skipping civility trim");
            return text;
        }

        // Auto-paste agents (Claude Code, Codex) treat "Děkuji"/"Prosím" as
        // dictation cruft, not chat content — strip it. Chat-style targets
        // (browsers, Ferdium, Gnome Text Editor) fall through unchanged.
        if (cliApp is null || !CliAgentRegistry.IsAutoPasteAgent(cliApp.AppName))
        {
            return text;
        }

        var stripped = CivilityTrimmer.StripTrailingCivility(text);
        if (stripped.Length != text.Length)
        {
            _logger.LogInformation(
                "Quick dictation: stripped trailing civility for {AppName} ({Before} → {After} chars)",
                cliApp.AppName, text.Length, stripped.Length);
        }
        return stripped;
    }
}
