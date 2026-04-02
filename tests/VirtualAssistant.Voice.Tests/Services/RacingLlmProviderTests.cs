using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Tests.Services;

public class RacingLlmProviderTests
{
    private readonly Mock<ILlmProviderFactory> _factoryMock;
    private readonly Mock<IPromptResolver> _promptResolverMock;
    private readonly Mock<ILlmProvider> _mercuryMock;
    private readonly Mock<ILlmProvider> _zenMock;
    private readonly Mock<ILogger<RacingLlmProvider>> _loggerMock;

    public RacingLlmProviderTests()
    {
        _factoryMock = new Mock<ILlmProviderFactory>();
        _promptResolverMock = new Mock<IPromptResolver>();
        _mercuryMock = new Mock<ILlmProvider>();
        _zenMock = new Mock<ILlmProvider>();
        _loggerMock = new Mock<ILogger<RacingLlmProvider>>();

        _mercuryMock.Setup(p => p.ProviderName).Returns("mercury");
        _mercuryMock.Setup(p => p.IsEnabled()).Returns(true);
        _zenMock.Setup(p => p.ProviderName).Returns("zen");
        _zenMock.Setup(p => p.IsEnabled()).Returns(true);

        _factoryMock.Setup(f => f.GetProvider("mercury")).Returns(_mercuryMock.Object);
        _factoryMock.Setup(f => f.GetProvider("zen")).Returns(_zenMock.Object);

        _promptResolverMock.Setup(r => r.ResolvePromptAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(("Test prompt text", 2));
    }

    private RacingLlmProvider CreateProvider(bool enabled = true, List<string>? providers = null)
    {
        var options = Options.Create(new LlmProviderOptions
        {
            ActiveProvider = "mercury",
            Racing = new RacingOptions
            {
                Enabled = enabled,
                Providers = providers ?? ["mercury", "zen"]
            }
        });
        return new RacingLlmProvider(_factoryMock.Object, _promptResolverMock.Object, options, _loggerMock.Object);
    }

    private static LlmCorrectionResult MakeResult(string text, int durationMs, int modelId = 1)
        => new(text, PromptId: 4, durationMs, ModelId: modelId);

    [Fact]
    public async Task BothSucceed_MercuryFaster_ReturnsMercuryAsWinner()
    {
        var mercuryTcs = new TaskCompletionSource<LlmCorrectionResult>();
        var zenTcs = new TaskCompletionSource<LlmCorrectionResult>();

        _mercuryMock.Setup(p => p.CorrectTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(mercuryTcs.Task);
        _zenMock.Setup(p => p.CorrectTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(zenTcs.Task);

        var provider = CreateProvider();
        var task = provider.CorrectTextAsync("test text");

        // Mercury completes first
        mercuryTcs.SetResult(MakeResult("Mercury result", 50, modelId: 18));
        var result = await task;

        Assert.NotNull(result);
        Assert.Equal("mercury", result.WinnerProviderName);
        Assert.Equal("Mercury result", result.WinnerResult.CorrectedText);
        Assert.Equal(50, result.WinnerResult.DurationMs);
        Assert.NotEqual(Guid.Empty, result.RaceGroupId);
        Assert.NotNull(result.LoserTask);
        Assert.Equal("zen", result.LoserProviderName);

        // Complete zen for cleanup
        zenTcs.SetResult(MakeResult("Zen result", 300, modelId: 20));
    }

    [Fact]
    public async Task BothSucceed_ZenFaster_ReturnsZenAsWinner()
    {
        var mercuryTcs = new TaskCompletionSource<LlmCorrectionResult>();
        var zenTcs = new TaskCompletionSource<LlmCorrectionResult>();

        _mercuryMock.Setup(p => p.CorrectTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(mercuryTcs.Task);
        _zenMock.Setup(p => p.CorrectTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(zenTcs.Task);

        var provider = CreateProvider();
        var task = provider.CorrectTextAsync("test text");

        // Zen completes first
        zenTcs.SetResult(MakeResult("Zen result", 50, modelId: 20));
        var result = await task;

        Assert.NotNull(result);
        Assert.Equal("zen", result.WinnerProviderName);
        Assert.Equal("Zen result", result.WinnerResult.CorrectedText);

        // Complete mercury for cleanup
        mercuryTcs.SetResult(MakeResult("Mercury result", 300, modelId: 18));
    }

    [Fact]
    public async Task MercuryFails_ZenSucceeds_ReturnsZenAsFallback()
    {
        _mercuryMock.Setup(p => p.CorrectTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Mercury timeout"));

        _zenMock.Setup(p => p.CorrectTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResult("Zen result", 100, modelId: 20));

        var provider = CreateProvider();
        var result = await provider.CorrectTextAsync("test text");

        Assert.NotNull(result);
        Assert.Equal("zen", result.WinnerProviderName);
        Assert.Equal("Zen result", result.WinnerResult.CorrectedText);
    }

    [Fact]
    public async Task ZenFails_MercurySucceeds_ReturnsMercuryAsFallback()
    {
        _mercuryMock.Setup(p => p.CorrectTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResult("Mercury result", 100, modelId: 18));

        _zenMock.Setup(p => p.CorrectTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Zen error"));

        var provider = CreateProvider();
        var result = await provider.CorrectTextAsync("test text");

        Assert.NotNull(result);
        Assert.Equal("mercury", result.WinnerProviderName);
    }

    [Fact]
    public async Task BothFail_ReturnsNull()
    {
        _mercuryMock.Setup(p => p.CorrectTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Mercury timeout"));

        _zenMock.Setup(p => p.CorrectTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Zen timeout"));

        var provider = CreateProvider();
        var result = await provider.CorrectTextAsync("test text");

        Assert.Null(result);
    }

    [Fact]
    public async Task SingleProvider_ReturnsResult()
    {
        _mercuryMock.Setup(p => p.CorrectTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResult("Mercury result", 100, modelId: 18));

        var provider = CreateProvider(providers: ["mercury"]);
        var result = await provider.CorrectTextAsync("test text");

        Assert.NotNull(result);
        Assert.Equal("mercury", result.WinnerProviderName);
        Assert.Null(result.LoserTask);
        Assert.Null(result.LoserProviderName);
    }

    [Fact]
    public async Task SingleProviderFails_ReturnsNull()
    {
        _mercuryMock.Setup(p => p.CorrectTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Mercury timeout"));

        var provider = CreateProvider(providers: ["mercury"]);
        var result = await provider.CorrectTextAsync("test text");

        Assert.Null(result);
    }

    [Fact]
    public async Task NoProvidersAvailable_ReturnsNull()
    {
        var provider = CreateProvider(providers: ["nonexistent"]);
        var result = await provider.CorrectTextAsync("test text");

        Assert.Null(result);
    }

    [Fact]
    public async Task LoserTaskCompletesWithResult()
    {
        var mercuryTcs = new TaskCompletionSource<LlmCorrectionResult>();
        var zenTcs = new TaskCompletionSource<LlmCorrectionResult>();

        _mercuryMock.Setup(p => p.CorrectTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(mercuryTcs.Task);
        _zenMock.Setup(p => p.CorrectTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(zenTcs.Task);

        var provider = CreateProvider();
        var task = provider.CorrectTextAsync("test text");

        // Mercury wins
        mercuryTcs.SetResult(MakeResult("Mercury result", 50, modelId: 18));
        var result = await task;

        Assert.NotNull(result);
        Assert.NotNull(result.LoserTask);

        // Zen completes later
        zenTcs.SetResult(MakeResult("Zen result", 200, modelId: 20));
        var loserResult = await result.LoserTask;

        Assert.NotNull(loserResult);
        Assert.Equal("Zen result", loserResult.CorrectedText);
        Assert.Equal(200, loserResult.DurationMs);
    }

    [Fact]
    public async Task RaceGroupId_IsConsistent()
    {
        _mercuryMock.Setup(p => p.CorrectTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResult("Mercury result", 50, modelId: 18));

        _zenMock.Setup(p => p.CorrectTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResult("Zen result", 100, modelId: 20));

        var provider = CreateProvider();
        var result = await provider.CorrectTextAsync("test text");

        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.RaceGroupId);

        // Two separate calls should have different RaceGroupIds
        var result2 = await provider.CorrectTextAsync("another text");
        Assert.NotNull(result2);
        Assert.NotEqual(result.RaceGroupId, result2.RaceGroupId);
    }

    [Fact]
    public async Task DisabledProvider_IsSkipped()
    {
        _zenMock.Setup(p => p.IsEnabled()).Returns(false);

        _mercuryMock.Setup(p => p.CorrectTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResult("Mercury result", 100, modelId: 18));

        var provider = CreateProvider();
        var result = await provider.CorrectTextAsync("test text");

        Assert.NotNull(result);
        Assert.Equal("mercury", result.WinnerProviderName);
        // Only one provider available, so no loser
        Assert.Null(result.LoserTask);
    }

    [Fact]
    public async Task PromptResolvedOnce_PassedToBothProviders()
    {
        _mercuryMock.Setup(p => p.CorrectTextAsync(It.IsAny<string>(), "Test prompt text", 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResult("Mercury result", 50, modelId: 18));

        _zenMock.Setup(p => p.CorrectTextAsync(It.IsAny<string>(), "Test prompt text", 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResult("Zen result", 100, modelId: 20));

        var provider = CreateProvider();
        var result = await provider.CorrectTextAsync("test text");

        Assert.NotNull(result);

        // Verify prompt was resolved exactly once
        _promptResolverMock.Verify(r => r.ResolvePromptAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Verify both providers received the same prompt
        _mercuryMock.Verify(p => p.CorrectTextAsync("test text", "Test prompt text", 2, It.IsAny<CancellationToken>()), Times.Once);
        _zenMock.Verify(p => p.CorrectTextAsync("test text", "Test prompt text", 2, It.IsAny<CancellationToken>()), Times.Once);
    }
}
