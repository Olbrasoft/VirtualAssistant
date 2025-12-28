using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Enums;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.TextInput;
using Olbrasoft.VirtualAssistant.Voice.Dtos;
using Olbrasoft.VirtualAssistant.Voice.Services;
using VirtualAssistant.Core.Services;

namespace VirtualAssistant.Voice.Tests.Services;

/// <summary>
/// Unit tests for ActionHandlerService.
/// Tests shared action handling logic used by multiple workers.
/// </summary>
public class ActionHandlerServiceTests
{
    private readonly Mock<ILogger<ActionHandlerService>> _loggerMock;
    private readonly Mock<ITextInputService> _textInputMock;
    private readonly Mock<IVirtualAssistantSpeaker> _speakerMock;
    private readonly Mock<IExternalServiceClient> _externalServiceMock;
    private readonly Mock<IRepeatTextIntentService> _repeatTextIntentMock;
    private readonly ActionHandlerService _sut;

    public ActionHandlerServiceTests()
    {
        _loggerMock = new Mock<ILogger<ActionHandlerService>>();
        _textInputMock = new Mock<ITextInputService>();
        _speakerMock = new Mock<IVirtualAssistantSpeaker>();
        _externalServiceMock = new Mock<IExternalServiceClient>();
        _repeatTextIntentMock = new Mock<IRepeatTextIntentService>();

        _sut = new ActionHandlerService(
            _loggerMock.Object,
            _textInputMock.Object,
            _speakerMock.Object,
            _externalServiceMock.Object,
            _repeatTextIntentMock.Object);
    }

    #region HandleOpenCodeActionAsync Tests

    [Theory]
    [InlineData(PromptType.Command, "build")]
    [InlineData(PromptType.Confirmation, "build")]
    [InlineData(PromptType.Continuation, "build")]
    public async Task HandleOpenCodeActionAsync_BuildPromptTypes_SendsToBuildAgent(PromptType promptType, string expectedAgent)
    {
        // Arrange
        const string command = "run tests";
        _textInputMock.Setup(x => x.SendMessageToSessionAsync(command, expectedAgent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _sut.HandleOpenCodeActionAsync(command, promptType, CancellationToken.None);

        // Assert
        _textInputMock.Verify(
            x => x.SendMessageToSessionAsync(command, expectedAgent, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(PromptType.Question, "plan")]
    [InlineData(PromptType.Acknowledgement, "plan")]
    [InlineData(null, "plan")]
    public async Task HandleOpenCodeActionAsync_PlanPromptTypes_SendsToPlanAgent(PromptType? promptType, string expectedAgent)
    {
        // Arrange
        const string command = "what is this code doing";
        _textInputMock.Setup(x => x.SendMessageToSessionAsync(command, expectedAgent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _sut.HandleOpenCodeActionAsync(command, promptType, CancellationToken.None);

        // Assert
        _textInputMock.Verify(
            x => x.SendMessageToSessionAsync(command, expectedAgent, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleOpenCodeActionAsync_Success_LogsInformation()
    {
        // Arrange
        const string command = "build the project";
        _textInputMock.Setup(x => x.SendMessageToSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _sut.HandleOpenCodeActionAsync(command, PromptType.Command, CancellationToken.None);

        // Assert - verify success was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Message sent to OpenCode")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleOpenCodeActionAsync_Failure_LogsWarning()
    {
        // Arrange
        const string command = "build the project";
        _textInputMock.Setup(x => x.SendMessageToSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _sut.HandleOpenCodeActionAsync(command, PromptType.Command, CancellationToken.None);

        // Assert - verify warning was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to send message to OpenCode")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region HandleRespondAction Tests

    [Fact]
    public void HandleRespondAction_ValidResponse_LogsInformation()
    {
        // Arrange
        const string response = "Hello, how can I help you?";

        // Act
        _sut.HandleRespondAction(response);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(response)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HandleRespondAction_NullOrEmptyResponse_LogsWarning(string? response)
    {
        // Act
        _sut.HandleRespondAction(response);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("LLM returned RESPOND but no response text")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region HandleRepeatTextAsync Tests

    [Fact]
    public async Task HandleRepeatTextAsync_Success_SpeaksClipboardResponse()
    {
        // Arrange
        const string expectedResponse = "Hotovo.";
        var pttResponse = new PttRepeatResponse("Sample text that was dictated", null);

        _externalServiceMock.Setup(x => x.CallPttRepeatAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, pttResponse, null));

        _repeatTextIntentMock.Setup(x => x.GetRandomClipboardResponse())
            .Returns(expectedResponse);

        // Act
        await _sut.HandleRepeatTextAsync(CancellationToken.None);

        // Assert
        _speakerMock.Verify(
            x => x.SpeakAsync(expectedResponse, null, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleRepeatTextAsync_SuccessWithLongText_LogsTruncatedPreview()
    {
        // Arrange
        var longText = new string('a', 100);
        var pttResponse = new PttRepeatResponse(longText, null);

        _externalServiceMock.Setup(x => x.CallPttRepeatAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, pttResponse, null));

        _repeatTextIntentMock.Setup(x => x.GetRandomClipboardResponse())
            .Returns("Hotovo.");

        // Act
        await _sut.HandleRepeatTextAsync(CancellationToken.None);

        // Assert - should log truncated version with ellipsis
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("...")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleRepeatTextAsync_NoTextInHistory_SpeaksWarningMessage()
    {
        // Arrange
        _externalServiceMock.Setup(x => x.CallPttRepeatAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, null, "No text in history"));

        // Act
        await _sut.HandleRepeatTextAsync(CancellationToken.None);

        // Assert
        _speakerMock.Verify(
            x => x.SpeakAsync("Zadny text v historii.", null, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleRepeatTextAsync_Error_SpeaksErrorMessage()
    {
        // Arrange
        _externalServiceMock.Setup(x => x.CallPttRepeatAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, null, "Connection timeout"));

        // Act
        await _sut.HandleRepeatTextAsync(CancellationToken.None);

        // Assert
        _speakerMock.Verify(
            x => x.SpeakAsync("Nepodarilo se ziskat text.", null, false, It.IsAny<CancellationToken>()),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Connection timeout")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region HandleDispatchTaskActionAsync Tests

    [Fact]
    public async Task HandleDispatchTaskActionAsync_SuccessWithIssueNumber_SpeaksIssueNumber()
    {
        // Arrange
        const string targetAgent = "claude";
        var response = new VoiceDispatchTaskResponse(
            Success: true,
            Reason: null,
            Message: "Task dispatched successfully",
            TaskId: 1,
            GithubIssueNumber: 123,
            GithubIssueUrl: "https://github.com/owner/repo/issues/123",
            Summary: "Fix bug in authentication");

        _externalServiceMock.Setup(x => x.DispatchTaskAsync(targetAgent, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, response, null));

        // Act
        await _sut.HandleDispatchTaskActionAsync(targetAgent, CancellationToken.None);

        // Assert
        _speakerMock.Verify(
            x => x.SpeakAsync("Posilam ukol cislo 123.", null, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleDispatchTaskActionAsync_SuccessWithoutIssueNumber_SpeaksGenericMessage()
    {
        // Arrange
        const string targetAgent = "claude";
        var response = new VoiceDispatchTaskResponse(
            Success: true,
            Reason: null,
            Message: "Task dispatched successfully",
            TaskId: 1,
            GithubIssueNumber: null,
            GithubIssueUrl: null,
            Summary: "General task");

        _externalServiceMock.Setup(x => x.DispatchTaskAsync(targetAgent, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, response, null));

        // Act
        await _sut.HandleDispatchTaskActionAsync(targetAgent, CancellationToken.None);

        // Assert
        _speakerMock.Verify(
            x => x.SpeakAsync("Ukol odeslan.", null, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleDispatchTaskActionAsync_AgentBusy_SpeaksBusyMessage()
    {
        // Arrange
        const string targetAgent = "claude";
        var response = new VoiceDispatchTaskResponse(
            Success: false,
            Reason: "agent_busy",
            Message: "Agent claude is currently busy",
            TaskId: null,
            GithubIssueNumber: null,
            GithubIssueUrl: null,
            Summary: null);

        _externalServiceMock.Setup(x => x.DispatchTaskAsync(targetAgent, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, response, null));

        // Act
        await _sut.HandleDispatchTaskActionAsync(targetAgent, CancellationToken.None);

        // Assert
        _speakerMock.Verify(
            x => x.SpeakAsync("claude je zaneprazdneny.", null, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleDispatchTaskActionAsync_NoPendingTasks_SpeaksNoTasksMessage()
    {
        // Arrange
        const string targetAgent = "claude";
        var response = new VoiceDispatchTaskResponse(
            Success: false,
            Reason: "no_pending_tasks",
            Message: "No pending tasks available",
            TaskId: null,
            GithubIssueNumber: null,
            GithubIssueUrl: null,
            Summary: null);

        _externalServiceMock.Setup(x => x.DispatchTaskAsync(targetAgent, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, response, null));

        // Act
        await _sut.HandleDispatchTaskActionAsync(targetAgent, CancellationToken.None);

        // Assert
        _speakerMock.Verify(
            x => x.SpeakAsync("Zadne cekajici ukoly.", null, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleDispatchTaskActionAsync_UnknownReason_SpeaksResponseMessage()
    {
        // Arrange
        const string targetAgent = "claude";
        const string customMessage = "Unknown error occurred";
        var response = new VoiceDispatchTaskResponse(
            Success: false,
            Reason: "unknown_error",
            Message: customMessage,
            TaskId: null,
            GithubIssueNumber: null,
            GithubIssueUrl: null,
            Summary: null);

        _externalServiceMock.Setup(x => x.DispatchTaskAsync(targetAgent, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, response, null));

        // Act
        await _sut.HandleDispatchTaskActionAsync(targetAgent, CancellationToken.None);

        // Assert
        _speakerMock.Verify(
            x => x.SpeakAsync(customMessage, null, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleDispatchTaskActionAsync_ResponseWithNullMessage_SpeaksFallbackMessage()
    {
        // Arrange
        const string targetAgent = "claude";
        var response = new VoiceDispatchTaskResponse(
            Success: false,
            Reason: "unknown_error",
            Message: null,
            TaskId: null,
            GithubIssueNumber: null,
            GithubIssueUrl: null,
            Summary: null);

        _externalServiceMock.Setup(x => x.DispatchTaskAsync(targetAgent, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, response, null));

        // Act
        await _sut.HandleDispatchTaskActionAsync(targetAgent, CancellationToken.None);

        // Assert
        _speakerMock.Verify(
            x => x.SpeakAsync("Nepodarilo se odeslat ukol.", null, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleDispatchTaskActionAsync_Error_SpeaksErrorMessage()
    {
        // Arrange
        const string targetAgent = "claude";
        _externalServiceMock.Setup(x => x.DispatchTaskAsync(targetAgent, It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, null, "Network error"));

        // Act
        await _sut.HandleDispatchTaskActionAsync(targetAgent, CancellationToken.None);

        // Assert
        _speakerMock.Verify(
            x => x.SpeakAsync("Chyba pri odesilani ukolu.", null, false, It.IsAny<CancellationToken>()),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Network error")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}
