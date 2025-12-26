using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Enums;
using Olbrasoft.VirtualAssistant.Core.Events;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.VirtualAssistant.Voice.Workers;

namespace VirtualAssistant.Voice.Tests.Workers;

/// <summary>
/// Unit tests for TranscriptionRouterWorker.
/// Tests transcription, echo cancellation, command detection, and LLM routing.
/// </summary>
public class TranscriptionRouterWorkerTests : IDisposable
{
    private readonly Mock<ILogger<TranscriptionRouterWorker>> _loggerMock;
    private readonly Mock<IEventBus> _eventBusMock;
    private readonly Mock<TranscriptionService> _transcriptionServiceMock;
    private readonly Mock<ILlmRouterService> _llmRouterMock;
    private readonly Mock<AssistantSpeechTrackerService> _speechTrackerMock;
    private readonly Mock<ICommandDetectionService> _commandDetectionMock;
    private readonly Mock<IRepeatTextIntentService> _repeatTextIntentMock;
    private readonly TranscriptionRouterWorker _sut;
    private Func<SpeechEndedEvent, CancellationToken, Task>? _speechEndedHandler;
    private Func<TranscriptionCancelledEvent, CancellationToken, Task>? _transcriptionCancelledHandler;

    public TranscriptionRouterWorkerTests()
    {
        _loggerMock = new Mock<ILogger<TranscriptionRouterWorker>>();
        _eventBusMock = new Mock<IEventBus>();
        _transcriptionServiceMock = new Mock<TranscriptionService>();
        _llmRouterMock = new Mock<ILlmRouterService>();
        _speechTrackerMock = new Mock<AssistantSpeechTrackerService>();
        _commandDetectionMock = new Mock<ICommandDetectionService>();
        _repeatTextIntentMock = new Mock<IRepeatTextIntentService>();

        // Capture event handlers
        _eventBusMock.Setup(x => x.Subscribe<SpeechEndedEvent>(It.IsAny<Func<SpeechEndedEvent, CancellationToken, Task>>()))
            .Callback<Func<SpeechEndedEvent, CancellationToken, Task>>(handler => _speechEndedHandler = handler)
            .Returns(Mock.Of<IDisposable>());

        _eventBusMock.Setup(x => x.Subscribe<TranscriptionCancelledEvent>(It.IsAny<Func<TranscriptionCancelledEvent, CancellationToken, Task>>()))
            .Callback<Func<TranscriptionCancelledEvent, CancellationToken, Task>>(handler => _transcriptionCancelledHandler = handler)
            .Returns(Mock.Of<IDisposable>());

        _sut = new TranscriptionRouterWorker(
            _loggerMock.Object,
            _eventBusMock.Object,
            _transcriptionServiceMock.Object,
            _llmRouterMock.Object,
            _speechTrackerMock.Object,
            _commandDetectionMock.Object,
            _repeatTextIntentMock.Object);
    }

    public void Dispose()
    {
        _sut?.Dispose();
    }

    [Fact]
    public void Constructor_SubscribesToEvents()
    {
        // Assert
        _eventBusMock.Verify(
            x => x.Subscribe<SpeechEndedEvent>(It.IsAny<Func<SpeechEndedEvent, CancellationToken, Task>>()),
            Times.Once);
        _eventBusMock.Verify(
            x => x.Subscribe<TranscriptionCancelledEvent>(It.IsAny<Func<TranscriptionCancelledEvent, CancellationToken, Task>>()),
            Times.Once);
    }

    [Fact]
    public async Task OnSpeechEnded_SuccessfulTranscription_PublishesTranscriptionCompletedEvent()
    {
        // Arrange
        var audioBuffer = new byte[] { 1, 2, 3, 4 };
        var @event = new SpeechEndedEvent(audioBuffer, 1500);
        var transcriptionResult = new TranscriptionResult("Hello world", 0.95f);

        _transcriptionServiceMock.Setup(x => x.TranscribeAsync(audioBuffer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transcriptionResult);
        _speechTrackerMock.Setup(x => x.FilterEchoFromTranscription("Hello world"))
            .Returns("Hello world");
        _commandDetectionMock.Setup(x => x.IsStopCommand("Hello world")).Returns(false);
        _repeatTextIntentMock.Setup(x => x.DetectIntentAsync("Hello world", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RepeatTextIntentResult { IsRepeatTextIntent = false, Confidence = 0.0f });
        _llmRouterMock.Setup(x => x.RouteAsync("Hello world", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmRouterResult
            {
                Action = LlmRouterAction.Respond,
                Response = "Hi there!",
                Confidence = 0.9f
            });

        // Act
        await _speechEndedHandler!(@event, CancellationToken.None);

        // Assert
        _eventBusMock.Verify(
            x => x.PublishAsync(
                It.Is<TranscriptionCompletedEvent>(e => e.Text == "Hello world" && e.DurationMs == 1500),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnSpeechEnded_FailedTranscription_DoesNotPublishEvents()
    {
        // Arrange
        var audioBuffer = new byte[] { 1, 2, 3, 4 };
        var @event = new SpeechEndedEvent(audioBuffer, 1000);
        var transcriptionResult = new TranscriptionResult("Transcription failed");

        _transcriptionServiceMock.Setup(x => x.TranscribeAsync(audioBuffer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transcriptionResult);

        // Act
        await _speechEndedHandler!(@event, CancellationToken.None);

        // Assert
        _eventBusMock.Verify(
            x => x.PublishAsync(It.IsAny<ActionRequestedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnSpeechEnded_EchoDetected_DoesNotRoute()
    {
        // Arrange
        var audioBuffer = new byte[] { 1, 2, 3, 4 };
        var @event = new SpeechEndedEvent(audioBuffer, 1000);
        var transcriptionResult = new TranscriptionResult("System started", 0.9f);

        _transcriptionServiceMock.Setup(x => x.TranscribeAsync(audioBuffer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transcriptionResult);
        _speechTrackerMock.Setup(x => x.FilterEchoFromTranscription("System started"))
            .Returns(""); // Echo filtered out

        // Act
        await _speechEndedHandler!(@event, CancellationToken.None);

        // Assert
        _llmRouterMock.Verify(
            x => x.RouteAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnSpeechEnded_StopCommand_DoesNotRoute()
    {
        // Arrange
        var audioBuffer = new byte[] { 1, 2, 3, 4 };
        var @event = new SpeechEndedEvent(audioBuffer, 1000);
        var transcriptionResult = new TranscriptionResult("stop", 0.95f);

        _transcriptionServiceMock.Setup(x => x.TranscribeAsync(audioBuffer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transcriptionResult);
        _speechTrackerMock.Setup(x => x.FilterEchoFromTranscription("stop"))
            .Returns("stop");
        _commandDetectionMock.Setup(x => x.IsStopCommand("stop")).Returns(true);

        // Act
        await _speechEndedHandler!(@event, CancellationToken.None);

        // Assert
        _llmRouterMock.Verify(
            x => x.RouteAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnSpeechEnded_RepeatTextIntent_PublishesDispatchTaskAction()
    {
        // Arrange
        var audioBuffer = new byte[] { 1, 2, 3, 4 };
        var @event = new SpeechEndedEvent(audioBuffer, 1000);
        var transcriptionResult = new TranscriptionResult("repeat last text", 0.9f);

        _transcriptionServiceMock.Setup(x => x.TranscribeAsync(audioBuffer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transcriptionResult);
        _speechTrackerMock.Setup(x => x.FilterEchoFromTranscription("repeat last text"))
            .Returns("repeat last text");
        _commandDetectionMock.Setup(x => x.IsStopCommand("repeat last text")).Returns(false);
        _repeatTextIntentMock.Setup(x => x.DetectIntentAsync("repeat last text", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RepeatTextIntentResult { IsRepeatTextIntent = true, Confidence = 0.85f });

        // Act
        await _speechEndedHandler!(@event, CancellationToken.None);

        // Assert
        _eventBusMock.Verify(
            x => x.PublishAsync(
                It.Is<ActionRequestedEvent>(e =>
                    e.Action == LlmRouterAction.DispatchTask &&
                    e.TargetAgent == "repeat-text"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _llmRouterMock.Verify(
            x => x.RouteAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnSpeechEnded_LlmRouting_PublishesActionRequestedEvent()
    {
        // Arrange
        var audioBuffer = new byte[] { 1, 2, 3, 4 };
        var @event = new SpeechEndedEvent(audioBuffer, 1200);
        var transcriptionResult = new TranscriptionResult("open code", 0.92f);

        _transcriptionServiceMock.Setup(x => x.TranscribeAsync(audioBuffer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transcriptionResult);
        _speechTrackerMock.Setup(x => x.FilterEchoFromTranscription("open code"))
            .Returns("open code");
        _commandDetectionMock.Setup(x => x.IsStopCommand("open code")).Returns(false);
        _repeatTextIntentMock.Setup(x => x.DetectIntentAsync("open code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RepeatTextIntentResult { IsRepeatTextIntent = false, Confidence = 0.1f });
        _llmRouterMock.Setup(x => x.RouteAsync("open code", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmRouterResult
            {
                Action = LlmRouterAction.OpenCode,
                PromptType = PromptType.Command,
                Confidence = 0.95f
            });

        // Act
        await _speechEndedHandler!(@event, CancellationToken.None);

        // Assert
        _eventBusMock.Verify(
            x => x.PublishAsync(
                It.Is<ActionRequestedEvent>(e =>
                    e.Action == LlmRouterAction.OpenCode &&
                    e.PromptType == PromptType.Command &&
                    e.OriginalText == "open code"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnTranscriptionCancelled_CancelsOngoingTranscription()
    {
        // Arrange - start a transcription
        var audioBuffer = new byte[] { 1, 2, 3, 4 };
        var speechEvent = new SpeechEndedEvent(audioBuffer, 1000);
        var tcs = new TaskCompletionSource<TranscriptionResult>();

        _transcriptionServiceMock.Setup(x => x.TranscribeAsync(audioBuffer, It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        var transcriptionTask = _speechEndedHandler!(speechEvent, CancellationToken.None);

        // Wait for transcription to start
        await Task.Delay(100);

        // Act - cancel it
        await _transcriptionCancelledHandler!(new TranscriptionCancelledEvent(), CancellationToken.None);
        await Task.Delay(100);

        // Complete the transcription with cancellation
        tcs.SetException(new OperationCanceledException());

        // Assert - should complete without throwing
        await transcriptionTask;
    }

    [Fact]
    public async Task OnSpeechEnded_AlreadyTranscribing_IgnoresNewSpeech()
    {
        // Arrange - start first transcription
        var audioBuffer1 = new byte[] { 1, 2, 3, 4 };
        var event1 = new SpeechEndedEvent(audioBuffer1, 1000);
        var tcs = new TaskCompletionSource<TranscriptionResult>();

        _transcriptionServiceMock.Setup(x => x.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        var task1 = _speechEndedHandler!(event1, CancellationToken.None);
        await Task.Delay(100);

        // Act - try to start second transcription
        var audioBuffer2 = new byte[] { 5, 6, 7, 8 };
        var event2 = new SpeechEndedEvent(audioBuffer2, 1200);
        await _speechEndedHandler!(event2, CancellationToken.None);

        // Assert - transcription should only be called once
        _transcriptionServiceMock.Verify(
            x => x.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Cleanup
        tcs.SetResult(new TranscriptionResult("test", 0.9f));
        await task1;
    }
}
