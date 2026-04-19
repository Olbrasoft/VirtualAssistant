using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Service.Hubs;
using Olbrasoft.VirtualAssistant.Service.Workers.Dictation;
using Olbrasoft.VirtualAssistant.Voice.StateMachine;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Workers.Dictation;

/// <summary>
/// Pins the post-recording pipeline lifted out of DictationWorker in #969.
/// Covers the transcribe → save → (quick: trim + paste + broadcast) /
/// (full: raise + type) → state-transition flow that used to live inline
/// in the worker's StopAndTranscribeAsync body.
/// </summary>
public class DictationCompletionPipelineTests
{
    private readonly Mock<ILogger<DictationCompletionPipeline>> _loggerMock = new();
    private readonly Mock<IDictationStateMachine> _stateMachineMock = new();
    private readonly Mock<IDictationTranscriber> _transcriberMock = new();
    private readonly Mock<IDictationOutputChannel> _outputChannelMock = new();
    private readonly Mock<IDictationTranscriptionPersister> _persisterMock = new();
    private readonly Mock<IClaudeCodeCivilityTrimmer> _civilityTrimmerMock = new();
    private readonly Mock<IDictationFocusRouter> _focusRouterMock = new();

    public DictationCompletionPipelineTests()
    {
        // Default civility trimmer: passthrough (non-Claude-Code path).
        // Individual tests override when they need the Claude-Code trim.
        _civilityTrimmerMock
            .Setup(x => x.TrimIfClaudeCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((text, _) => Task.FromResult(text));

        // Default focus router: no-op (no switch). Individual tests override
        // when they need to assert the router was consulted / fired.
        _focusRouterMock
            .Setup(x => x.TryFocusClaudeCodeIfApplicableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private DictationCompletionPipeline CreateSut() =>
        new(_loggerMock.Object, _stateMachineMock.Object, _transcriberMock.Object,
            _outputChannelMock.Object, _persisterMock.Object, _civilityTrimmerMock.Object,
            _focusRouterMock.Object);

    [Fact]
    public async Task CompleteQuickAsync_Success_PastesAndBroadcasts()
    {
        var audio = new byte[] { 1, 2, 3 };
        _transcriberMock.Setup(x => x.TranscribeRawAsync(audio, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult("quick text", 0.9f));
        _outputChannelMock.Setup(x => x.FastPasteAsync("quick text", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateSut().CompleteQuickAsync(audio, CancellationToken.None);

        _outputChannelMock.Verify(x => x.StartTypingFeedback(), Times.Once);
        _persisterMock.Verify(
            x => x.SaveAsync(audio, It.Is<TranscriptionResult>(r => r.Text == "quick text"), It.IsAny<CancellationToken>()),
            Times.Once);
        _outputChannelMock.Verify(x => x.FastPasteAsync("quick text", It.IsAny<CancellationToken>()), Times.Once);
        _outputChannelMock.Verify(
            x => x.BroadcastEventAsync(DictationEventType.QuickTranscriptionCompleted, "quick text", It.IsAny<CancellationToken>()),
            Times.Once);
        _outputChannelMock.Verify(x => x.StopTypingFeedback(), Times.Once);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.Once);
    }

    [Fact]
    public async Task CompleteQuickAsync_TranscriptionEmpty_SkipsPaste()
    {
        var audio = new byte[] { 1 };
        _transcriberMock.Setup(x => x.TranscribeRawAsync(audio, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult("") { OriginalText = null });

        await CreateSut().CompleteQuickAsync(audio, CancellationToken.None);

        _outputChannelMock.Verify(x => x.FastPasteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _persisterMock.Verify(
            x => x.SaveAsync(It.IsAny<byte[]>(), It.IsAny<TranscriptionResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _outputChannelMock.Verify(x => x.StopTypingFeedback(), Times.Once);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.Once);
    }

    [Fact]
    public async Task CompleteQuickAsync_CivilityTrimmedToEmpty_SkipsPaste()
    {
        // Claude-Code civility trim strips "Děkuji." hallucination; if it
        // reduces the whole text to empty the pipeline must skip paste,
        // not send an empty keystroke burst.
        var audio = new byte[] { 1 };
        _transcriberMock.Setup(x => x.TranscribeRawAsync(audio, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult("Děkuji.", 0.9f));
        _civilityTrimmerMock
            .Setup(x => x.TrimIfClaudeCodeAsync("Děkuji.", It.IsAny<CancellationToken>()))
            .ReturnsAsync("   ");

        await CreateSut().CompleteQuickAsync(audio, CancellationToken.None);

        _outputChannelMock.Verify(x => x.FastPasteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _outputChannelMock.Verify(
            x => x.BroadcastEventAsync(It.IsAny<DictationEventType>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.Once);
    }

    [Fact]
    public async Task CompleteQuickAsync_ConsultsFocusRouterBeforeCivilityTrim()
    {
        // Ordering contract: focus router runs BEFORE civility trim so the
        // trimmer's "am I in Claude Code?" heuristic sees the post-focus
        // state. If we trimmed first, then focus-switched into Claude Code,
        // the civility "Děkuji." sign-off could leak into the prompt.
        var audio = new byte[] { 1 };
        _transcriberMock.Setup(x => x.TranscribeRawAsync(audio, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult("quick text", 0.9f));
        _outputChannelMock.Setup(x => x.FastPasteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var order = new List<string>();
        _focusRouterMock
            .Setup(x => x.TryFocusClaudeCodeIfApplicableAsync(It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("focus"))
            .ReturnsAsync(false);
        _civilityTrimmerMock
            .Setup(x => x.TrimIfClaudeCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((_, _) => order.Add("trim"))
            .Returns<string, CancellationToken>((text, _) => Task.FromResult(text));

        await CreateSut().CompleteQuickAsync(audio, CancellationToken.None);

        _focusRouterMock.Verify(x => x.TryFocusClaudeCodeIfApplicableAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(new[] { "focus", "trim" }, order);
    }

    [Fact]
    public async Task CompleteFullAsync_DoesNotConsultFocusRouter()
    {
        // Normal Dictation keeps today's behavior: it types into the active
        // window as-is, with no auto-focus switching. The router is Quick-only.
        var audio = new byte[] { 1 };
        _transcriberMock.Setup(x => x.TranscribeFullAsync(audio, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult("full text", 0.9f));
        _outputChannelMock.Setup(x => x.TypeIntoActiveWindowAsync("full text", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateSut().CompleteFullAsync(audio, _ => { }, CancellationToken.None);

        _focusRouterMock.Verify(
            x => x.TryFocusClaudeCodeIfApplicableAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CompleteQuickAsync_FastPasteFails_DoesNotBroadcast()
    {
        // Regression for PR #1025 review: if the keystroke burst didn't land,
        // the remote UI must NOT see QuickTranscriptionCompleted (otherwise it
        // would light up as if the text went through).
        var audio = new byte[] { 1 };
        _transcriberMock.Setup(x => x.TranscribeRawAsync(audio, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult("quick text", 0.9f));
        _outputChannelMock.Setup(x => x.FastPasteAsync("quick text", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await CreateSut().CompleteQuickAsync(audio, CancellationToken.None);

        _outputChannelMock.Verify(
            x => x.BroadcastEventAsync(DictationEventType.QuickTranscriptionCompleted, It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.Once);
    }

    [Fact]
    public async Task CompleteFullAsync_Success_SavesAndTypes()
    {
        var audio = new byte[] { 1, 2 };
        var transcription = new TranscriptionResult("hello world", 0.95f)
        {
            OriginalText = "hello world",
            SttProviderId = 14,
            PromptId = 42
        };
        _transcriberMock.Setup(x => x.TranscribeFullAsync(audio, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transcription);
        _outputChannelMock.Setup(x => x.TypeIntoActiveWindowAsync("hello world", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var callbackText = (string?)null;
        await CreateSut().CompleteFullAsync(audio, text => callbackText = text, CancellationToken.None);

        // Forwards the raw TranscriptionResult (incl. LLM metadata) to the
        // persister; LlmCorrectionResult details live in the persister.
        _persisterMock.Verify(
            x => x.SaveAsync(
                audio,
                It.Is<TranscriptionResult>(r => r.Text == "hello world" && r.SttProviderId == 14 && r.PromptId == 42),
                It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal("hello world", callbackText);
        _outputChannelMock.Verify(x => x.TypeIntoActiveWindowAsync("hello world", It.IsAny<CancellationToken>()), Times.Once);
        _outputChannelMock.Verify(x => x.StopTypingFeedback(), Times.Once);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.Once);
    }

    [Fact]
    public async Task CompleteFullAsync_TranscriptionEmpty_SkipsSaveAndType()
    {
        var audio = new byte[] { 1 };
        _transcriberMock.Setup(x => x.TranscribeFullAsync(audio, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult("") { OriginalText = null });

        var callbackFired = false;
        await CreateSut().CompleteFullAsync(audio, _ => callbackFired = true, CancellationToken.None);

        _persisterMock.Verify(
            x => x.SaveAsync(It.IsAny<byte[]>(), It.IsAny<TranscriptionResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.False(callbackFired);
        _outputChannelMock.Verify(
            x => x.TypeIntoActiveWindowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.Once);
    }

    [Fact]
    public async Task CompleteFullAsync_TypingFails_StillTransitionsIdle()
    {
        var audio = new byte[] { 1 };
        _transcriberMock.Setup(x => x.TranscribeFullAsync(audio, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult("text", 0.9f));
        _outputChannelMock.Setup(x => x.TypeIntoActiveWindowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await CreateSut().CompleteFullAsync(audio, _ => { }, CancellationToken.None);

        _outputChannelMock.Verify(x => x.StopTypingFeedback(), Times.Once);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.Once);
    }

    [Fact]
    public async Task CompleteFullAsync_CallbackFiresBeforeTyping()
    {
        // The callback is the worker's TranscriptionCompleted event raise;
        // it must fire BEFORE typing so the remote-UI SignalR broadcast
        // lands ahead of the keystrokes.
        var audio = new byte[] { 1 };
        _transcriberMock.Setup(x => x.TranscribeFullAsync(audio, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult("text", 0.9f));

        var order = new List<string>();
        _outputChannelMock.Setup(x => x.TypeIntoActiveWindowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("type"))
            .ReturnsAsync(true);

        await CreateSut().CompleteFullAsync(audio, _ => order.Add("callback"), CancellationToken.None);

        Assert.Equal(new[] { "callback", "type" }, order);
    }

    [Fact]
    public async Task CompleteQuickAsync_TranscriberThrows_StillStopsFeedbackAndIdles()
    {
        // Copilot review on PR #1031: the pipeline's contract says it owns
        // StopTypingFeedback + Idle transition on every exit path, so if an
        // awaited dep throws (incl. cancellation), the finally block must
        // still run. Otherwise the typing sound loops forever and the state
        // machine stays stuck on Transcribing.
        _transcriberMock.Setup(x => x.TranscribeRawAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CreateSut().CompleteQuickAsync(new byte[] { 1 }, CancellationToken.None));

        _outputChannelMock.Verify(x => x.StopTypingFeedback(), Times.Once);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.Once);
    }

    [Fact]
    public async Task CompleteFullAsync_TranscriberThrows_StillStopsFeedbackAndIdles()
    {
        _transcriberMock.Setup(x => x.TranscribeFullAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSut().CompleteFullAsync(new byte[] { 1 }, _ => { }, CancellationToken.None));

        _outputChannelMock.Verify(x => x.StopTypingFeedback(), Times.Once);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.Once);
    }

    [Fact]
    public async Task CompleteQuickAsync_NullAudio_Throws() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateSut().CompleteQuickAsync(null!, CancellationToken.None));

    [Fact]
    public async Task CompleteFullAsync_NullAudio_Throws() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateSut().CompleteFullAsync(null!, _ => { }, CancellationToken.None));

    [Fact]
    public async Task CompleteFullAsync_NullCallback_Throws() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateSut().CompleteFullAsync(new byte[] { 1 }, null!, CancellationToken.None));
}
