using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;

namespace Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

/// <inheritdoc />
public sealed class ClaudeCodeCivilityTrimmer : IClaudeCodeCivilityTrimmer
{
    private const string ClaudeCodeAppName = "Claude Code";

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

        if (cliApp is null || !string.Equals(cliApp.AppName, ClaudeCodeAppName, StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        var stripped = CivilityTrimmer.StripTrailingCivility(text);
        if (stripped.Length != text.Length)
        {
            _logger.LogInformation(
                "Quick dictation: stripped trailing civility for Claude Code ({Before} → {After} chars)",
                text.Length, stripped.Length);
        }
        return stripped;
    }
}
