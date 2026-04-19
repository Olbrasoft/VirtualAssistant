using Microsoft.Extensions.DependencyInjection;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.Speech;

namespace Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

/// <inheritdoc />
public sealed class DictationTranscriptionPersister : IDictationTranscriptionPersister
{
    // provider_id has a FK constraint on the transcriptions table; when the
    // active transcriber hasn't set SttProviderId (legacy code path) we fall
    // back to Whisper so the insert doesn't violate the constraint.
    private const int WhisperProviderId = 13;

    private readonly IServiceScopeFactory _scopeFactory;

    public DictationTranscriptionPersister(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    public async Task SaveAsync(byte[] audioData, TranscriptionResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audioData);
        ArgumentNullException.ThrowIfNull(result);

        // DictationWorker is a singleton but IDictationPersistenceService is
        // scoped (per-DbContext) — create a fresh scope for each save.
        using var scope = _scopeFactory.CreateScope();
        var persistenceService = scope.ServiceProvider.GetRequiredService<IDictationPersistenceService>();

        var originalText = result.OriginalText ?? result.Text;
        var correctionResult = BuildCorrectionResult(result);
        var sttProviderId = result.SttProviderId.GetValueOrDefault(WhisperProviderId);

        if (result.RaceGroupId.HasValue)
        {
            await persistenceService.SaveTranscriptionWithRacingAsync(
                audioData,
                originalText,
                correctionResult,
                sttProviderId,
                result.RaceGroupId.Value,
                result.RacingLoserTask,
                cancellationToken);
        }
        else
        {
            await persistenceService.SaveTranscriptionAsync(
                audioData,
                originalText,
                correctionResult,
                sttProviderId,
                cancellationToken);
        }
    }

    private static LlmCorrectionResult? BuildCorrectionResult(TranscriptionResult result)
    {
        // Only build a correction result when the LLM actually corrected the
        // text (OriginalText differs from Text AND we have a model id AND
        // non-zero duration). Partial or missing metadata means no correction
        // ran — persistence treats null as "no correction".
        var hasCorrection = result.OriginalText != null
            && result.Text != result.OriginalText;

        if (!hasCorrection || !result.ModelId.HasValue || result.LlmDurationMs.GetValueOrDefault() <= 0)
        {
            return null;
        }

        return new LlmCorrectionResult(
            CorrectedText: result.Text,
            PromptId: result.PromptId,
            DurationMs: result.LlmDurationMs.Value,
            ModelId: result.ModelId,
            InputTokens: result.InputTokens,
            OutputTokens: result.OutputTokens,
            ReasoningTokens: result.ReasoningTokens);
    }
}
