using Olbrasoft.VirtualAssistant.Core.Speech;

namespace Olbrasoft.VirtualAssistant.Core.Tests.Speech;

/// <summary>
/// Pins the two-constructor contract on TranscriptionResult: the success
/// constructor owns Text/Confidence and nulls ErrorMessage, the failure
/// constructor zeroes out transcription fields and surfaces the error.
/// Init-only metadata (LLM duration, token counts, race info) must be
/// assignable on both shapes so the pipeline can enrich either outcome.
/// </summary>
public class TranscriptionResultTests
{
    [Fact]
    public void SuccessConstructor_SetsTextAndConfidence_AndMarksSuccess()
    {
        var result = new TranscriptionResult("hello world", 0.95f);

        Assert.Equal("hello world", result.Text);
        Assert.Equal(0.95f, result.Confidence);
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void FailureConstructor_ZeroesTextAndConfidence_AndSurfacesError()
    {
        var result = new TranscriptionResult("whisper timed out");

        Assert.Equal(string.Empty, result.Text);
        Assert.Equal(0.0f, result.Confidence);
        Assert.False(result.Success);
        Assert.Equal("whisper timed out", result.ErrorMessage);
    }

    [Fact]
    public void InitOnlyMetadata_FlowsThroughOnSuccess()
    {
        var raceId = Guid.NewGuid();
        var loserTask = Task.FromResult<Olbrasoft.VirtualAssistant.Core.Models.LlmCorrectionResult?>(null);

        var result = new TranscriptionResult("done", 0.82f)
        {
            OriginalText = "raw",
            FilteredText = "filtered",
            LlmDurationMs = 120,
            PromptId = 7,
            ModelId = 3,
            SttProviderId = 11,
            InputTokens = 40,
            OutputTokens = 8,
            ReasoningTokens = 2,
            RaceGroupId = raceId,
            RacingLoserTask = loserTask,
            RacingLoserProviderName = "zen",
        };

        Assert.Equal("raw", result.OriginalText);
        Assert.Equal("filtered", result.FilteredText);
        Assert.Equal(120, result.LlmDurationMs);
        Assert.Equal(7, result.PromptId);
        Assert.Equal(3, result.ModelId);
        Assert.Equal(11, result.SttProviderId);
        Assert.Equal(40, result.InputTokens);
        Assert.Equal(8, result.OutputTokens);
        Assert.Equal(2, result.ReasoningTokens);
        Assert.Equal(raceId, result.RaceGroupId);
        Assert.Same(loserTask, result.RacingLoserTask);
        Assert.Equal("zen", result.RacingLoserProviderName);
    }

    [Fact]
    public void InitOnlyMetadata_DefaultsToNull_WhenNotSet()
    {
        var result = new TranscriptionResult("x", 0.5f);

        Assert.Null(result.OriginalText);
        Assert.Null(result.FilteredText);
        Assert.Null(result.LlmDurationMs);
        Assert.Null(result.PromptId);
        Assert.Null(result.ModelId);
        Assert.Null(result.SttProviderId);
        Assert.Null(result.InputTokens);
        Assert.Null(result.OutputTokens);
        Assert.Null(result.ReasoningTokens);
        Assert.Null(result.RaceGroupId);
        Assert.Null(result.RacingLoserTask);
        Assert.Null(result.RacingLoserProviderName);
    }

    [Fact]
    public void SuccessConstructor_AcceptsEmptyText_AndZeroConfidence()
    {
        // The pipeline emits empty-text successes for silent audio; pin that
        // this doesn't get conflated with the failure path.
        var result = new TranscriptionResult(string.Empty, 0f);

        Assert.Equal(string.Empty, result.Text);
        Assert.Equal(0f, result.Confidence);
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
    }
}
