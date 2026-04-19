using Olbrasoft.VirtualAssistant.Core.Models;

namespace Olbrasoft.VirtualAssistant.Core.Tests.Models;

/// <summary>
/// Coverage pass for the plain data records under
/// <c>src/VirtualAssistant.Core/Models</c>: properties assign verbatim,
/// optional params have the documented defaults, and record equality
/// treats same-valued instances as equal. These models cross every
/// service boundary (desktop context, LLM results, TTS results,
/// notification routing) so pinning their shape catches accidental
/// required/optional flips in one place. Part of #979 Phase C.
/// </summary>
public class ModelRecordsTests
{
    [Fact]
    public void DesktopContext_CtorAssignsAllProperties()
    {
        var ts = new DateTime(2026, 4, 19, 10, 0, 0, DateTimeKind.Utc);
        var sut = new DesktopContext(
            CurrentWorkspace: 2,
            TotalWorkspaces: 4,
            ActiveWindowTitle: "VSCode",
            ActiveWindowClass: "code",
            ActiveApplication: "code",
            Timestamp: ts);

        Assert.Equal(2, sut.CurrentWorkspace);
        Assert.Equal(4, sut.TotalWorkspaces);
        Assert.Equal("VSCode", sut.ActiveWindowTitle);
        Assert.Equal("code", sut.ActiveWindowClass);
        Assert.Equal("code", sut.ActiveApplication);
        Assert.Equal(ts, sut.Timestamp);
    }

    [Fact]
    public void DesktopContext_RecordEquality_SameValues_AreEqual()
    {
        var ts = DateTime.UtcNow;
        var a = new DesktopContext(1, 2, "t", "c", "app", ts);
        var b = new DesktopContext(1, 2, "t", "c", "app", ts);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Theory]
    [InlineData(ChangeType.WorkspaceChanged)]
    [InlineData(ChangeType.WindowFocusChanged)]
    [InlineData(ChangeType.ApplicationChanged)]
    public void DesktopContextChange_RetainsChangeType(ChangeType type)
    {
        var ts = DateTime.UtcNow;
        var prev = new DesktopContext(0, 1, "", "", "", ts);
        var next = new DesktopContext(1, 1, "", "", "", ts);

        var change = new DesktopContextChange(prev, next, type);

        Assert.Same(prev, change.PreviousContext);
        Assert.Same(next, change.NewContext);
        Assert.Equal(type, change.Type);
    }

    [Fact]
    public void WorkspaceChangedEventArgs_AssignsIndexes()
    {
        var args = new WorkspaceChangedEventArgs(NewIndex: 2, TotalWorkspaces: 6);

        Assert.Equal(2, args.NewIndex);
        Assert.Equal(6, args.TotalWorkspaces);
    }

    [Fact]
    public void FocusChangedEventArgs_AssignsWindowMetadata()
    {
        var args = new FocusChangedEventArgs("Title", "app.id", "WMClass");

        Assert.Equal("Title", args.WindowTitle);
        Assert.Equal("app.id", args.AppId);
        Assert.Equal("WMClass", args.WmClass);
    }

    [Fact]
    public void LlmCorrectionResult_RequiredFieldsAssigned_OptionalFieldsDefaultNull()
    {
        var sut = new LlmCorrectionResult(
            CorrectedText: "hi",
            PromptId: 7,
            DurationMs: 123);

        Assert.Equal("hi", sut.CorrectedText);
        Assert.Equal(7, sut.PromptId);
        Assert.Equal(123, sut.DurationMs);
        Assert.Null(sut.ModelId);
        Assert.Null(sut.InputTokens);
        Assert.Null(sut.OutputTokens);
        Assert.Null(sut.ReasoningTokens);
    }

    [Fact]
    public void LlmCorrectionResult_AllOptionalFieldsAssigned()
    {
        var sut = new LlmCorrectionResult(
            CorrectedText: "ok",
            PromptId: null,
            DurationMs: 42,
            ModelId: 9,
            InputTokens: 120,
            OutputTokens: 80,
            ReasoningTokens: 5);

        Assert.Null(sut.PromptId);
        Assert.Equal(9, sut.ModelId);
        Assert.Equal(120, sut.InputTokens);
        Assert.Equal(80, sut.OutputTokens);
        Assert.Equal(5, sut.ReasoningTokens);
    }

    [Fact]
    public void TtsResult_SuccessPath_CarriesProviderAndDuration()
    {
        var sut = new TtsResult(Success: true, ProviderUsed: "azure", DurationMs: 850);

        Assert.True(sut.Success);
        Assert.Equal("azure", sut.ProviderUsed);
        Assert.Equal(850, sut.DurationMs);
        Assert.False(sut.Skipped);
        Assert.False(sut.Cancelled);
    }

    [Fact]
    public void TtsResult_SkippedOrCancelled_DefaultsProviderAndDurationToNull()
    {
        var skipped = new TtsResult(Success: false, Skipped: true);
        var cancelled = new TtsResult(Success: false, Cancelled: true);

        Assert.True(skipped.Skipped);
        Assert.Null(skipped.ProviderUsed);
        Assert.Null(skipped.DurationMs);
        Assert.True(cancelled.Cancelled);
        Assert.Null(cancelled.ProviderUsed);
    }

    [Fact]
    public async Task RacingResult_DualProvider_CarriesWinnerAndLoserTask()
    {
        var winner = new LlmCorrectionResult("winner", null, 10);
        var loserResult = new LlmCorrectionResult("loser", null, 20);
        var raceId = Guid.NewGuid();

        var sut = new RacingResult(
            WinnerResult: winner,
            WinnerProviderName: "mercury",
            RaceGroupId: raceId,
            LoserTask: Task.FromResult<LlmCorrectionResult?>(loserResult),
            LoserProviderName: "zen");

        Assert.Same(winner, sut.WinnerResult);
        Assert.Equal("mercury", sut.WinnerProviderName);
        Assert.Equal(raceId, sut.RaceGroupId);
        Assert.Equal("zen", sut.LoserProviderName);
        Assert.NotNull(sut.LoserTask);
        var awaited = await sut.LoserTask!;
        Assert.Same(loserResult, awaited);
    }

    [Fact]
    public void RacingResult_SingleProvider_LoserTaskAndNameAreNull()
    {
        var winner = new LlmCorrectionResult("solo", null, 5);

        var sut = new RacingResult(winner, "mercury", Guid.NewGuid(), null, null);

        Assert.Null(sut.LoserTask);
        Assert.Null(sut.LoserProviderName);
    }

    [Theory]
    [InlineData(NotificationSource.TaskCompletion, true, "code")]
    [InlineData(NotificationSource.GitHubEvent, false, "chrome")]
    [InlineData(NotificationSource.SystemAlert, true, null)]
    [InlineData(NotificationSource.UserMessage, false, null)]
    public void NotificationContext_AssignsAllFields(NotificationSource source, bool urgent, string? target)
    {
        var sut = new NotificationContext(target, urgent, source);

        Assert.Equal(target, sut.TargetApplication);
        Assert.Equal(urgent, sut.IsUrgent);
        Assert.Equal(source, sut.Source);
    }

    [Fact]
    public void AgentNotification_AssignsRequiredAndOptionalFields()
    {
        var sut = new AgentNotification
        {
            NotificationId = 42,
            Agent = "claude-code",
            Type = "status",
            Content = "done"
        };

        Assert.Equal(42, sut.NotificationId);
        Assert.Equal("claude-code", sut.Agent);
        Assert.Equal("status", sut.Type);
        Assert.Equal("done", sut.Content);
    }

    [Fact]
    public void AgentNotification_NotificationIdOptional_DefaultsNull()
    {
        var sut = new AgentNotification
        {
            Agent = "gemini",
            Type = "alert",
            Content = "hello"
        };

        Assert.Null(sut.NotificationId);
    }
}
