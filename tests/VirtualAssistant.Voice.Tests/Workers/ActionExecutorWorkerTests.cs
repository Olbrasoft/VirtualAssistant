using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Enums;
using Olbrasoft.VirtualAssistant.Core.Events;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.TextInput;
using Olbrasoft.VirtualAssistant.Voice.Dtos;
using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.VirtualAssistant.Voice.Workers;
using OpenCode.DotnetClient;
using VirtualAssistant.Core.Services;

namespace VirtualAssistant.Voice.Tests.Workers;

/// <summary>
/// Unit tests for ActionExecutorWorker.
/// Tests action execution based on LLM routing results.
/// </summary>
public class ActionExecutorWorkerTests : IDisposable
{
    private readonly Mock<ILogger<ActionExecutorWorker>> _loggerMock;
    private readonly Mock<IEventBus> _eventBusMock;
    private readonly Mock<ITextInputService> _textInputServiceMock;
    private readonly Mock<IVirtualAssistantSpeaker> _speakerMock;
    private readonly Mock<IExternalServiceClient> _externalServiceMock;
    private readonly Mock<IRepeatTextIntentService> _repeatTextIntentMock;
    private readonly ActionExecutorWorker _sut;
    private Func<ActionRequestedEvent, CancellationToken, Task>? _actionRequestedHandler;

    public ActionExecutorWorkerTests()
    {
        _loggerMock = new Mock<ILogger<ActionExecutorWorker>>();
        _eventBusMock = new Mock<IEventBus>();
        _textInputServiceMock = new Mock<ITextInputService>();
        _speakerMock = new Mock<IVirtualAssistantSpeaker>();
        _externalServiceMock = new Mock<IExternalServiceClient>();
        _repeatTextIntentMock = new Mock<IRepeatTextIntentService>();

        // Capture action requested handler
        _eventBusMock.Setup(x => x.Subscribe<ActionRequestedEvent>(It.IsAny<Func<ActionRequestedEvent, CancellationToken, Task>>()))
            .Callback<Func<ActionRequestedEvent, CancellationToken, Task>>(handler => _actionRequestedHandler = handler)
            .Returns(Mock.Of<IDisposable>());

        _sut = new ActionExecutorWorker(
            _loggerMock.Object,
            _eventBusMock.Object,
            _textInputServiceMock.Object,
            _speakerMock.Object,
            _externalServiceMock.Object,
            _repeatTextIntentMock.Object);
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
    public async Task OnActionRequested_OpenCode_SendsMessageWithBuildAgent()
    {
        // Arrange
        var @event = new ActionRequestedEvent(
            LlmRouterAction.OpenCode,
            "run tests",
            PromptType: PromptType.Command);

        _textInputServiceMock.Setup(x => x.SendMessageToSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _actionRequestedHandler!(@event, CancellationToken.None);

        // Assert
        _textInputServiceMock.Verify(
            x => x.SendMessageToSessionAsync("run tests", "build", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnActionRequested_OpenCode_WithQuestionPrompt_UsesPlanAgent()
    {
        // Arrange
        var @event = new ActionRequestedEvent(
            LlmRouterAction.OpenCode,
            "what is this code doing",
            PromptType: PromptType.Question);

        _textInputServiceMock.Setup(x => x.SendMessageToSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _actionRequestedHandler!(@event, CancellationToken.None);

        // Assert
        _textInputServiceMock.Verify(
            x => x.SendMessageToSessionAsync("what is this code doing", "plan", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnActionRequested_Bash_SendsMessageWithBuildAgent()
    {
        // Arrange
        var @event = new ActionRequestedEvent(
            LlmRouterAction.Bash,
            "ls -la",
            PromptType: PromptType.Command);

        _textInputServiceMock.Setup(x => x.SendMessageToSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _actionRequestedHandler!(@event, CancellationToken.None);

        // Assert
        _textInputServiceMock.Verify(
            x => x.SendMessageToSessionAsync("ls -la", "build", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnActionRequested_Respond_LogsResponse()
    {
        // Arrange
        var @event = new ActionRequestedEvent(
            LlmRouterAction.Respond,
            "hello",
            Response: "Hi there!");

        // Act
        await _actionRequestedHandler!(@event, CancellationToken.None);

        // Assert - should just log, no other calls
        _textInputServiceMock.VerifyNoOtherCalls();
        _speakerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OnActionRequested_RespondWithoutText_LogsWarning()
    {
        // Arrange
        var @event = new ActionRequestedEvent(
            LlmRouterAction.Respond,
            "hello",
            Response: null);

        // Act
        await _actionRequestedHandler!(@event, CancellationToken.None);

        // Assert - should complete without errors
        _textInputServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OnActionRequested_RepeatText_CallsPttEndpoint()
    {
        // Arrange
        var @event = new ActionRequestedEvent(
            LlmRouterAction.DispatchTask,
            "repeat last text",
            TargetAgent: "repeat-text");

        _externalServiceMock.Setup(x => x.CallPttRepeatAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new PttRepeatResponse("Hello world", null), null));
        _repeatTextIntentMock.Setup(x => x.GetRandomClipboardResponse())
            .Returns("Text copied to clipboard");

        // Act
        await _actionRequestedHandler!(@event, CancellationToken.None);

        // Assert
        _externalServiceMock.Verify(x => x.CallPttRepeatAsync(It.IsAny<CancellationToken>()), Times.Once);
        _speakerMock.Verify(
            x => x.SpeakAsync("Text copied to clipboard", null, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnActionRequested_RepeatText_NoHistory_SpeaksError()
    {
        // Arrange
        var @event = new ActionRequestedEvent(
            LlmRouterAction.DispatchTask,
            "repeat last text",
            TargetAgent: "repeat-text");

        _externalServiceMock.Setup(x => x.CallPttRepeatAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, null, "No text in history"));

        // Act
        await _actionRequestedHandler!(@event, CancellationToken.None);

        // Assert
        _speakerMock.Verify(
            x => x.SpeakAsync("Zadny text v historii.", null, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnActionRequested_DispatchTask_Success_SpeaksConfirmation()
    {
        // Arrange
        var @event = new ActionRequestedEvent(
            LlmRouterAction.DispatchTask,
            "do something",
            TargetAgent: "claude");

        _externalServiceMock.Setup(x => x.DispatchTaskAsync("claude", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new VoiceDispatchTaskResponse(true, null, null, null, 123, null, null), null));

        // Act
        await _actionRequestedHandler!(@event, CancellationToken.None);

        // Assert
        _externalServiceMock.Verify(x => x.DispatchTaskAsync("claude", It.IsAny<CancellationToken>()), Times.Once);
        _speakerMock.Verify(
            x => x.SpeakAsync("Posilam ukol cislo 123.", null, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnActionRequested_DispatchTask_AgentBusy_SpeaksMessage()
    {
        // Arrange
        var @event = new ActionRequestedEvent(
            LlmRouterAction.DispatchTask,
            "do something",
            TargetAgent: "claude");

        _externalServiceMock.Setup(x => x.DispatchTaskAsync("claude", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new VoiceDispatchTaskResponse(false, "agent_busy", "Agent is currently busy", null, null, null, null), null));

        // Act
        await _actionRequestedHandler!(@event, CancellationToken.None);

        // Assert
        _speakerMock.Verify(
            x => x.SpeakAsync("claude je zaneprazdneny.", null, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnActionRequested_DispatchTask_NoPendingTasks_SpeaksMessage()
    {
        // Arrange
        var @event = new ActionRequestedEvent(
            LlmRouterAction.DispatchTask,
            "do something",
            TargetAgent: "claude");

        _externalServiceMock.Setup(x => x.DispatchTaskAsync("claude", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new VoiceDispatchTaskResponse(false, "no_pending_tasks", "No pending tasks available", null, null, null, null), null));

        // Act
        await _actionRequestedHandler!(@event, CancellationToken.None);

        // Assert
        _speakerMock.Verify(
            x => x.SpeakAsync("Zadne cekajici ukoly.", null, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnActionRequested_SaveNote_LogsNotImplemented()
    {
        // Arrange
        var @event = new ActionRequestedEvent(
            LlmRouterAction.SaveNote,
            "remember to buy milk");

        // Act
        await _actionRequestedHandler!(@event, CancellationToken.None);

        // Assert - should just log
        _textInputServiceMock.VerifyNoOtherCalls();
        _speakerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OnActionRequested_Ignore_DoesNothing()
    {
        // Arrange
        var @event = new ActionRequestedEvent(
            LlmRouterAction.Ignore,
            "hmm");

        // Act
        await _actionRequestedHandler!(@event, CancellationToken.None);

        // Assert
        _textInputServiceMock.VerifyNoOtherCalls();
        _speakerMock.VerifyNoOtherCalls();
        _externalServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OnActionRequested_Exception_LogsError()
    {
        // Arrange
        var @event = new ActionRequestedEvent(
            LlmRouterAction.OpenCode,
            "test",
            PromptType: PromptType.Command);

        _textInputServiceMock.Setup(x => x.SendMessageToSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        await _actionRequestedHandler!(@event, CancellationToken.None);

        // Assert - should complete without throwing
    }
}
