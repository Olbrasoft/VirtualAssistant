using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;
using Olbrasoft.VirtualAssistant.Data.Entities;
using Olbrasoft.VirtualAssistant.Data.Queries.PromptQueries;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Tests.Services;

/// <summary>
/// Unit tests for the priority cascade in <see cref="SystemPromptResolver"/>.
/// Previously this logic was embedded in LlmProviderBase.GetSystemPromptAsync
/// and exercised indirectly through provider tests; now that the resolver is
/// its own service, each branch deserves a dedicated test.
/// </summary>
public class SystemPromptResolverTests : IDisposable
{
    private readonly Mock<IPromptCache> _promptCacheMock = new();
    private readonly Mock<IDesktopContextService> _desktopContextServiceMock = new();
    private readonly Mock<ICliAppDetector> _cliAppDetectorMock = new();
    private readonly Mock<IQueryProcessor> _queryProcessorMock = new();
    private readonly Mock<ILogger<SystemPromptResolver>> _loggerMock = new();
    private readonly ServiceProvider _serviceProvider;
    private readonly SystemPromptResolver _sut;

    public SystemPromptResolverTests()
    {
        _desktopContextServiceMock
            .Setup(x => x.GetCurrentContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DesktopContext(0, 1, "window", "cls", "app", DateTime.UtcNow));

        // Real scope factory that resolves the mocked IQueryProcessor per-scope —
        // same shape as production so the captive-dependency fix is actually exercised.
        var services = new ServiceCollection();
        services.AddScoped(_ => _queryProcessorMock.Object);
        _serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });

        _sut = new SystemPromptResolver(
            _promptCacheMock.Object,
            _desktopContextServiceMock.Object,
            _cliAppDetectorMock.Object,
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _loggerMock.Object);
    }

    public void Dispose() => _serviceProvider.Dispose();

    [Fact]
    public async Task ResolveAsync_CliAppDetected_ReturnsCliAppPrompt()
    {
        var cliApp = new CliAppDetectionResult(AppName: "Claude Code", PromptFileName: "ClaudeCodeCorrection");
        _cliAppDetectorMock.Setup(x => x.DetectCliAppAsync(It.IsAny<CancellationToken>())).ReturnsAsync(cliApp);
        _queryProcessorMock
            .Setup(x => x.ProcessAsync(It.Is<GetPromptByFileNameQuery>(q => q.PromptFileName == "ClaudeCodeCorrection"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Prompt { Id = 42, PromptFileName = "ClaudeCodeCorrection" });
        _promptCacheMock.Setup(x => x.GetPrompt("ClaudeCodeCorrection")).Returns("cli prompt text");

        var (text, id) = await _sut.ResolveAsync();

        Assert.Equal("cli prompt text", text);
        Assert.Equal(42, id);
    }

    [Fact]
    public async Task ResolveAsync_CliAppDetectedButPromptMissingFromDb_FallsThroughToWindowPattern()
    {
        var cliApp = new CliAppDetectionResult(AppName: "Claude Code", PromptFileName: "ClaudeCodeCorrection");
        _cliAppDetectorMock.Setup(x => x.DetectCliAppAsync(It.IsAny<CancellationToken>())).ReturnsAsync(cliApp);
        _queryProcessorMock
            .Setup(x => x.ProcessAsync(It.IsAny<GetPromptByFileNameQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Prompt?)null);
        _queryProcessorMock
            .Setup(x => x.ProcessAsync(It.IsAny<GetPromptByAppIdPatternQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Prompt { Id = 7, PromptFileName = "WindowPattern" });
        _promptCacheMock.Setup(x => x.GetPrompt("WindowPattern")).Returns("window pattern text");

        var (text, id) = await _sut.ResolveAsync();

        Assert.Equal("window pattern text", text);
        Assert.Equal(7, id);
    }

    [Fact]
    public async Task ResolveAsync_NoCliApp_ReturnsWindowPatternPrompt()
    {
        _cliAppDetectorMock.Setup(x => x.DetectCliAppAsync(It.IsAny<CancellationToken>())).ReturnsAsync((CliAppDetectionResult?)null);
        _queryProcessorMock
            .Setup(x => x.ProcessAsync(It.IsAny<GetPromptByAppIdPatternQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Prompt { Id = 5, PromptFileName = "EditorCorrection" });
        _promptCacheMock.Setup(x => x.GetPrompt("EditorCorrection")).Returns("editor prompt");

        var (text, id) = await _sut.ResolveAsync();

        Assert.Equal("editor prompt", text);
        Assert.Equal(5, id);
    }

    [Fact]
    public async Task ResolveAsync_NoCliAppNoWindowMatch_ReturnsDefaultPrompt()
    {
        _cliAppDetectorMock.Setup(x => x.DetectCliAppAsync(It.IsAny<CancellationToken>())).ReturnsAsync((CliAppDetectionResult?)null);
        _queryProcessorMock
            .Setup(x => x.ProcessAsync(It.IsAny<GetPromptByAppIdPatternQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Prompt?)null);
        _queryProcessorMock
            .Setup(x => x.ProcessAsync(It.IsAny<GetDefaultPromptQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Prompt { Id = 1, PromptFileName = "DefaultCorrection" });
        _promptCacheMock.Setup(x => x.GetPrompt("DefaultCorrection")).Returns("default");

        var (text, id) = await _sut.ResolveAsync();

        Assert.Equal("default", text);
        Assert.Equal(1, id);
    }

    [Fact]
    public async Task ResolveAsync_AllDbLookupsFail_UsesHardcodedFallback()
    {
        _cliAppDetectorMock.Setup(x => x.DetectCliAppAsync(It.IsAny<CancellationToken>())).ReturnsAsync((CliAppDetectionResult?)null);
        _queryProcessorMock
            .Setup(x => x.ProcessAsync(It.IsAny<GetPromptByAppIdPatternQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Prompt?)null);
        _queryProcessorMock
            .Setup(x => x.ProcessAsync(It.IsAny<GetDefaultPromptQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Prompt?)null);
        _promptCacheMock.Setup(x => x.GetPrompt("DefaultCorrection")).Returns("hardcoded fallback");

        var (text, id) = await _sut.ResolveAsync();

        Assert.Equal("hardcoded fallback", text);
        Assert.Equal(4, id); // documented fallback ID
    }

    [Fact]
    public async Task ResolveAsync_CancellationRequested_PropagatesOperationCanceledException()
    {
        // Cancellation must not silently return the fallback prompt — the caller
        // explicitly asked to stop, and should see OperationCanceledException.
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _cliAppDetectorMock
            .Setup(x => x.DetectCliAppAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(() => _sut.ResolveAsync(cts.Token));
    }

    [Fact]
    public async Task ResolveAsync_NonCancellationException_FallsBackToDefault()
    {
        // Unexpected errors should still be caught and the fallback returned
        // — dictation must not crash because of a transient DB hiccup.
        _cliAppDetectorMock
            .Setup(x => x.DetectCliAppAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB blew up"));
        _promptCacheMock.Setup(x => x.GetPrompt("DefaultCorrection")).Returns("hardcoded fallback");

        var (text, id) = await _sut.ResolveAsync();

        Assert.Equal("hardcoded fallback", text);
        Assert.Equal(4, id);
    }
}
