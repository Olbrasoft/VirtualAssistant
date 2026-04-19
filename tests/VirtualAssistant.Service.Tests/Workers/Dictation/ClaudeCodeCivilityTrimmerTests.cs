using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;
using Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Workers.Dictation;

/// <summary>
/// Pins the Claude-Code scoping the civility trimmer enforces: in chat apps
/// "Děkuji." is a legitimate message and must pass through untouched, in
/// Claude Code it's a Whisper hallucination that would poison the prompt.
/// Detection errors must fall back to "no trim" — one stray civility word
/// is cheaper than mangling valid input when gdbus hiccups.
/// </summary>
public class ClaudeCodeCivilityTrimmerTests
{
    private readonly Mock<ILogger<ClaudeCodeCivilityTrimmer>> _loggerMock = new();
    private readonly Mock<ICliAppDetector> _detectorMock = new();

    private ClaudeCodeCivilityTrimmer CreateSut() =>
        new(_loggerMock.Object, _detectorMock.Object);

    [Fact]
    public async Task TrimIfClaudeCodeAsync_EmptyText_ReturnsTextUnchanged_WithoutProbing()
    {
        var sut = CreateSut();

        var result = await sut.TrimIfClaudeCodeAsync(string.Empty, CancellationToken.None);

        Assert.Equal(string.Empty, result);
        _detectorMock.Verify(x => x.DetectCliAppAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TrimIfClaudeCodeAsync_ClaudeCodeActive_StripsTrailingCivility()
    {
        // CivilityTrimmer only matches a whole trailing SENTENCE against its
        // phrase list, so the input must contain a real sentence boundary.
        _detectorMock
            .Setup(x => x.DetectCliAppAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliAppDetectionResult("Claude Code", "claude.md"));

        var sut = CreateSut();
        var result = await sut.TrimIfClaudeCodeAsync("Run the build. Děkuji.", CancellationToken.None);

        Assert.DoesNotContain("Děkuji", result);
        Assert.Equal("Run the build.", result.TrimEnd());
    }

    [Fact]
    public async Task TrimIfClaudeCodeAsync_OtherCliApp_ReturnsTextUnchanged()
    {
        _detectorMock
            .Setup(x => x.DetectCliAppAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliAppDetectionResult("OpenCode", "opencode.md"));

        var sut = CreateSut();
        var result = await sut.TrimIfClaudeCodeAsync("hello Děkuji.", CancellationToken.None);

        Assert.Equal("hello Děkuji.", result);
    }

    [Fact]
    public async Task TrimIfClaudeCodeAsync_NoCliAppDetected_ReturnsTextUnchanged()
    {
        _detectorMock
            .Setup(x => x.DetectCliAppAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CliAppDetectionResult?)null);

        var sut = CreateSut();
        var result = await sut.TrimIfClaudeCodeAsync("hello Děkuji.", CancellationToken.None);

        Assert.Equal("hello Děkuji.", result);
    }

    [Fact]
    public async Task TrimIfClaudeCodeAsync_DetectorThrows_ReturnsTextUnchanged()
    {
        _detectorMock
            .Setup(x => x.DetectCliAppAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("gdbus broken"));

        var sut = CreateSut();
        var result = await sut.TrimIfClaudeCodeAsync("hello Děkuji.", CancellationToken.None);

        Assert.Equal("hello Děkuji.", result);
    }

    [Fact]
    public async Task TrimIfClaudeCodeAsync_DetectorCancelled_Propagates()
    {
        _detectorMock
            .Setup(x => x.DetectCliAppAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var sut = CreateSut();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.TrimIfClaudeCodeAsync("hello Děkuji.", CancellationToken.None));
    }

    [Fact]
    public async Task TrimIfClaudeCodeAsync_AppNameMatchIsCaseInsensitive()
    {
        // CliAppDetector has normalized casing in the past — pin that the
        // trimmer does its own case-insensitive compare so a lowercase
        // "claude code" from a future detection strategy still triggers.
        _detectorMock
            .Setup(x => x.DetectCliAppAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CliAppDetectionResult("claude code", "claude.md"));

        var sut = CreateSut();
        var result = await sut.TrimIfClaudeCodeAsync("Build. Děkuji.", CancellationToken.None);

        Assert.DoesNotContain("Děkuji", result);
    }
}
