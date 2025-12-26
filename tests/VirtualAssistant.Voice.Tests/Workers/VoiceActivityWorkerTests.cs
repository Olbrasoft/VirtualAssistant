using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Events;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.VirtualAssistant.Voice.Workers;

namespace VirtualAssistant.Voice.Tests.Workers;

/// <summary>
/// Unit tests for VoiceActivityWorker.
/// Tests VAD processing, buffer management, and state transitions.
/// </summary>
public class VoiceActivityWorkerTests : IDisposable
{
    private readonly Mock<ILogger<VoiceActivityWorker>> _loggerMock;
    private readonly Mock<IEventBus> _eventBusMock;
    private readonly Mock<VadService> _vadServiceMock;
    private readonly Mock<IVoiceStateMachine> _stateMachineMock;
    private readonly Mock<ISpeechBufferManager> _bufferManagerMock;
    private readonly IOptions<ContinuousListenerOptions> _options;
    private readonly VoiceActivityWorker _sut;
    private Func<AudioChunkCapturedEvent, CancellationToken, Task>? _audioChunkHandler;

    public VoiceActivityWorkerTests()
    {
        _loggerMock = new Mock<ILogger<VoiceActivityWorker>>();
        _eventBusMock = new Mock<IEventBus>();
        _vadServiceMock = new Mock<VadService>();
        _stateMachineMock = new Mock<IVoiceStateMachine>();
        _bufferManagerMock = new Mock<ISpeechBufferManager>();

        _options = Options.Create(new ContinuousListenerOptions
        {
            PostSilenceMs = 1500,
            MinRecordingMs = 800
        });

        // Capture the audio chunk handler
        _eventBusMock.Setup(x => x.Subscribe<AudioChunkCapturedEvent>(It.IsAny<Func<AudioChunkCapturedEvent, CancellationToken, Task>>()))
            .Callback<Func<AudioChunkCapturedEvent, CancellationToken, Task>>(handler => _audioChunkHandler = handler)
            .Returns(Mock.Of<IDisposable>());

        _sut = new VoiceActivityWorker(
            _loggerMock.Object,
            _eventBusMock.Object,
            _vadServiceMock.Object,
            _stateMachineMock.Object,
            _bufferManagerMock.Object,
            _options);
    }

    public void Dispose()
    {
        _sut?.Dispose();
    }

    [Fact]
    public void Constructor_SubscribesToAudioChunkEvents()
    {
        // Assert
        _eventBusMock.Verify(
            x => x.Subscribe<AudioChunkCapturedEvent>(It.IsAny<Func<AudioChunkCapturedEvent, CancellationToken, Task>>()),
            Times.Once);
    }

    [Fact]
    public async Task OnAudioChunk_InWaitingState_WithSpeech_StartsRecording()
    {
        // Arrange
        var audioData = new byte[] { 1, 2, 3, 4 };
        var @event = new AudioChunkCapturedEvent(audioData, 0.5f);

        _stateMachineMock.Setup(x => x.CurrentState).Returns(VoiceState.Waiting);
        _vadServiceMock.Setup(x => x.Analyze(audioData)).Returns((true, 0.8f));

        // Act
        await _audioChunkHandler!(@event, CancellationToken.None);

        // Assert
        _stateMachineMock.Verify(x => x.StartRecording(0.8f), Times.Once);
        _bufferManagerMock.Verify(x => x.TransferPreBufferToSpeech(), Times.Once);
        _bufferManagerMock.Verify(x => x.AddToSpeechBuffer(audioData), Times.Once);
        _eventBusMock.Verify(
            x => x.PublishAsync(It.IsAny<SpeechDetectedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnAudioChunk_InWaitingState_WithoutSpeech_AddsToPreBuffer()
    {
        // Arrange
        var audioData = new byte[] { 1, 2, 3, 4 };
        var @event = new AudioChunkCapturedEvent(audioData, 0.1f);

        _stateMachineMock.Setup(x => x.CurrentState).Returns(VoiceState.Waiting);
        _vadServiceMock.Setup(x => x.Analyze(audioData)).Returns((false, 0.1f));

        // Act
        await _audioChunkHandler!(@event, CancellationToken.None);

        // Assert
        _bufferManagerMock.Verify(x => x.AddToPreBuffer(audioData), Times.Once);
        _stateMachineMock.Verify(x => x.StartRecording(It.IsAny<float>()), Times.Never);
    }

    [Fact]
    public async Task OnAudioChunk_InRecordingState_WithSpeech_ResetsSilenceTimer()
    {
        // Arrange
        var audioData = new byte[] { 1, 2, 3, 4 };
        var @event = new AudioChunkCapturedEvent(audioData, 0.6f);

        _stateMachineMock.Setup(x => x.CurrentState).Returns(VoiceState.Recording);
        _stateMachineMock.SetupProperty(x => x.SilenceStartTime, DateTime.UtcNow);
        _vadServiceMock.Setup(x => x.Analyze(audioData)).Returns((true, 0.6f));

        // Act
        await _audioChunkHandler!(@event, CancellationToken.None);

        // Assert
        _bufferManagerMock.Verify(x => x.AddToSpeechBuffer(audioData), Times.Once);
        Assert.Equal(default(DateTime), _stateMachineMock.Object.SilenceStartTime);
    }

    [Fact]
    public async Task OnAudioChunk_InRecordingState_WithSilence_TracksSilenceDuration()
    {
        // Arrange
        var audioData = new byte[] { 1, 2, 3, 4 };
        var @event = new AudioChunkCapturedEvent(audioData, 0.05f);

        _stateMachineMock.Setup(x => x.CurrentState).Returns(VoiceState.Recording);
        _stateMachineMock.SetupProperty(x => x.SilenceStartTime, default(DateTime));
        _vadServiceMock.Setup(x => x.Analyze(audioData)).Returns((false, 0.05f));

        // Act
        await _audioChunkHandler!(@event, CancellationToken.None);

        // Assert
        _bufferManagerMock.Verify(x => x.AddToSpeechBuffer(audioData), Times.Once);
        Assert.NotEqual(default(DateTime), _stateMachineMock.Object.SilenceStartTime);
    }

    [Fact]
    public async Task OnAudioChunk_InRecordingState_AfterMaxSilence_CompletesRecording()
    {
        // Arrange
        var audioData = new byte[] { 1, 2, 3, 4 };
        var @event = new AudioChunkCapturedEvent(audioData, 0.02f);
        var combinedAudio = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        _stateMachineMock.Setup(x => x.CurrentState).Returns(VoiceState.Recording);
        _stateMachineMock.Setup(x => x.RecordingStartTime).Returns(DateTime.UtcNow.AddSeconds(-2)); // 2000ms ago
        _stateMachineMock.SetupProperty(x => x.SilenceStartTime, DateTime.UtcNow.AddSeconds(-2)); // 2000ms silence
        _vadServiceMock.Setup(x => x.Analyze(audioData)).Returns((false, 0.02f));
        _bufferManagerMock.Setup(x => x.GetCombinedSpeechData()).Returns(combinedAudio);

        // Act
        await _audioChunkHandler!(@event, CancellationToken.None);

        // Assert
        _stateMachineMock.Verify(x => x.ResetToWaiting(), Times.Once);
        _bufferManagerMock.Verify(x => x.ClearSpeechBuffer(), Times.Once);
        _eventBusMock.Verify(
            x => x.PublishAsync(
                It.Is<SpeechEndedEvent>(e => e.AudioBuffer == combinedAudio && e.DurationMs >= 2000),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnAudioChunk_InRecordingState_TooShortSpeech_ResetsWithoutPublishing()
    {
        // Arrange
        var audioData = new byte[] { 1, 2, 3, 4 };
        var @event = new AudioChunkCapturedEvent(audioData, 0.02f);

        _stateMachineMock.Setup(x => x.CurrentState).Returns(VoiceState.Recording);
        _stateMachineMock.Setup(x => x.RecordingStartTime).Returns(DateTime.UtcNow.AddMilliseconds(-500)); // Only 500ms
        _stateMachineMock.SetupProperty(x => x.SilenceStartTime, DateTime.UtcNow.AddSeconds(-2)); // 2000ms silence
        _vadServiceMock.Setup(x => x.Analyze(audioData)).Returns((false, 0.02f));

        // Act
        await _audioChunkHandler!(@event, CancellationToken.None);

        // Assert
        _stateMachineMock.Verify(x => x.ResetToWaiting(), Times.Once);
        _bufferManagerMock.Verify(x => x.ClearSpeechBuffer(), Times.Once);
        _eventBusMock.Verify(
            x => x.PublishAsync(It.IsAny<SpeechEndedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnAudioChunk_InMutedState_DoesNothing()
    {
        // Arrange
        var audioData = new byte[] { 1, 2, 3, 4 };
        var @event = new AudioChunkCapturedEvent(audioData, 0.8f);

        _stateMachineMock.Setup(x => x.CurrentState).Returns(VoiceState.Muted);
        _vadServiceMock.Setup(x => x.Analyze(audioData)).Returns((true, 0.8f));

        // Act
        await _audioChunkHandler!(@event, CancellationToken.None);

        // Assert
        _bufferManagerMock.VerifyNoOtherCalls();
        _stateMachineMock.Verify(x => x.StartRecording(It.IsAny<float>()), Times.Never);
    }
}
