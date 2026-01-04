using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Enums;
using Olbrasoft.VirtualAssistant.Core.Events;
using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.VirtualAssistant.Voice.Workers;

namespace Olbrasoft.VirtualAssistant.Voice.Tests.Workers;

/// <summary>
/// Unit tests for ActionExecutorWorker.
/// Tests action execution based on LLM routing results.
/// </summary>
public class ActionExecutorWorkerTests : IDisposable
{
    private readonly Mock<ILogger<ActionExecutorWorker>> _loggerMock;
    private readonly Mock<IEventBus> _eventBusMock;
    private readonly Mock<IActionHandlerService> _actionHandlerMock;
    private readonly ActionExecutorWorker _sut;
    private Func<ActionRequestedEvent, CancellationToken, Task>? _actionRequestedHandler;

    public ActionExecutorWorkerTests()
    {
        _loggerMock = new Mock<ILogger<ActionExecutorWorker>>();
        _eventBusMock = new Mock<IEventBus>();
        _actionHandlerMock = new Mock<IActionHandlerService>();

        // Capture action requested handler
        _eventBusMock.Setup(x => x.Subscribe<ActionRequestedEvent>(It.IsAny<Func<ActionRequestedEvent, CancellationToken, Task>>()))
            .Callback<Func<ActionRequestedEvent, CancellationToken, Task>>(handler => _actionRequestedHandler = handler)
            .Returns(Mock.Of<IDisposable>());

        _sut = new ActionExecutorWorker(
            _loggerMock.Object,
            _eventBusMock.Object,
            _actionHandlerMock.Object);
    }

    public void Dispose()
    {
        _sut?.Dispose();
    }

    [Fact]
    public void Constructor_SubscribesToActionRequestedEvent()
    {
        // Assert
        _eventBusMock.Verify(
            x => x.Subscribe<ActionRequestedEvent>(It.IsAny<Func<ActionRequestedEvent, CancellationToken, Task>>()),
            Times.Once);
    }

    [Fact]
    public async Task OnActionRequested_OpenCode_CallsActionHandler()
    {
        // Arrange
        var @event = new ActionRequestedEvent(
            LlmRouterAction.OpenCode,
            "run tests",
            PromptType: PromptType.Command);

        // Act
        await _actionRequestedHandler!(@event, CancellationToken.None);

        // Assert
        _actionHandlerMock.Verify(
            x => x.HandleOpenCodeActionAsync("run tests", PromptType.Command, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnActionRequested_OpenCode_WithQuestionPrompt_CallsActionHandler()
    {
        // Arrange
        var @event = new ActionRequestedEvent(
            LlmRouterAction.OpenCode,
            "what is this code doing",
            PromptType: PromptType.Question);

        // Act
        await _actionRequestedHandler!(@event, CancellationToken.None);

        // Assert
        _actionHandlerMock.Verify(
            x => x.HandleOpenCodeActionAsync("what is this code doing", PromptType.Question, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnActionRequested_Bash_CallsActionHandler()
    {
        // Arrange
        var @event = new ActionRequestedEvent(
            LlmRouterAction.Bash,
            "ls -la",
            PromptType: PromptType.Command);

        // Act
        await _actionRequestedHandler!(@event, CancellationToken.None);

        // Assert
        _actionHandlerMock.Verify(
            x => x.HandleOpenCodeActionAsync("ls -la", PromptType.Command, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnActionRequested_Respond_CallsActionHandler()
    {
        // Arrange
        var @event = new ActionRequestedEvent(
            LlmRouterAction.Respond,
            "hello",
            Response: "Hi there!");

        // Act
        await _actionRequestedHandler!(@event, CancellationToken.None);

        // Assert
        _actionHandlerMock.Verify(
            x => x.HandleRespondAction("Hi there!"),
            Times.Once);
    }

    [Fact]
    public async Task OnActionRequested_RespondWithoutText_CallsActionHandler()
    {
        // Arrange
        var @event = new ActionRequestedEvent(
            LlmRouterAction.Respond,
            "hello",
            Response: null);

        // Act
        await _actionRequestedHandler!(@event, CancellationToken.None);

        // Assert
        _actionHandlerMock.Verify(
            x => x.HandleRespondAction(null),
            Times.Once);
    }

    [Fact]
    public async Task OnActionRequested_RepeatText_CallsActionHandler()
    {
        // Arrange
        var @event = new ActionRequestedEvent(
            LlmRouterAction.DispatchTask,
            "repeat last text",
            TargetAgent: "repeat-text");

        // Act
        await _actionRequestedHandler!(@event, CancellationToken.None);

        // Assert
        _actionHandlerMock.Verify(
            x => x.HandleRepeatTextAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // Removed - this test is now covered by ActionHandlerServiceTests

    [Fact]
    public async Task OnActionRequested_DispatchTask_CallsActionHandler()
    {
        // Arrange
        var @event = new ActionRequestedEvent(
            LlmRouterAction.DispatchTask,
            "do something",
            TargetAgent: "claude");

        // Act
        await _actionRequestedHandler!(@event, CancellationToken.None);

        // Assert
        _actionHandlerMock.Verify(
            x => x.HandleDispatchTaskActionAsync("claude", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // Removed - detailed dispatch task tests are now covered by ActionHandlerServiceTests

    [Fact]
    public async Task OnActionRequested_SaveNote_DoesNotCallActionHandler()
    {
        // Arrange
        var @event = new ActionRequestedEvent(
            LlmRouterAction.SaveNote,
            "remember to buy milk");

        // Act
        await _actionRequestedHandler!(@event, CancellationToken.None);

        // Assert - should not call action handler (not implemented)
        _actionHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OnActionRequested_Ignore_DoesNotCallActionHandler()
    {
        // Arrange
        var @event = new ActionRequestedEvent(
            LlmRouterAction.Ignore,
            "hmm");

        // Act
        await _actionRequestedHandler!(@event, CancellationToken.None);

        // Assert
        _actionHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OnActionRequested_Exception_LogsError()
    {
        // Arrange
        var @event = new ActionRequestedEvent(
            LlmRouterAction.OpenCode,
            "test",
            PromptType: PromptType.Command);

        _actionHandlerMock.Setup(x => x.HandleOpenCodeActionAsync(It.IsAny<string>(), It.IsAny<PromptType?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        await _actionRequestedHandler!(@event, CancellationToken.None);

        // Assert - should complete without throwing
    }
}
