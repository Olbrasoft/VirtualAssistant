using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Service.Hubs;
using Olbrasoft.VirtualAssistant.Voice.StateMachine;

namespace Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

/// <inheritdoc />
public sealed class DictationCompletionPipeline : IDictationCompletionPipeline
{
    private readonly ILogger<DictationCompletionPipeline> _logger;
    private readonly IDictationStateMachine _stateMachine;
    private readonly IDictationTranscriber _transcriber;
    private readonly IDictationOutputChannel _outputChannel;
    private readonly IDictationTranscriptionPersister _persister;
    private readonly IClaudeCodeCivilityTrimmer _civilityTrimmer;

    public DictationCompletionPipeline(
        ILogger<DictationCompletionPipeline> logger,
        IDictationStateMachine stateMachine,
        IDictationTranscriber transcriber,
        IDictationOutputChannel outputChannel,
        IDictationTranscriptionPersister persister,
        IClaudeCodeCivilityTrimmer civilityTrimmer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _transcriber = transcriber ?? throw new ArgumentNullException(nameof(transcriber));
        _outputChannel = outputChannel ?? throw new ArgumentNullException(nameof(outputChannel));
        _persister = persister ?? throw new ArgumentNullException(nameof(persister));
        _civilityTrimmer = civilityTrimmer ?? throw new ArgumentNullException(nameof(civilityTrimmer));
    }

    public async Task CompleteQuickAsync(byte[] audio, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audio);

        _outputChannel.StartTypingFeedback();

        var result = await _transcriber.TranscribeRawAsync(audio, cancellationToken);

        if (!result.Success || string.IsNullOrWhiteSpace(result.Text))
        {
            _logger.LogWarning("Quick transcription failed or empty");
            _outputChannel.StopTypingFeedback();
            _stateMachine.TransitionTo(DictationState.Idle);
            return;
        }

        _logger.LogInformation("Quick transcription: '{Text}'", result.Text);

        await _persister.SaveAsync(audio, result, cancellationToken);

        // Strip trailing civility ("… Děkuji.", "… Ahoj.", Whisper hallucinated
        // sign-offs) when pasting into a CLI agent that reads the text as a
        // prompt. Scoped to Claude Code only — in chat apps "Děkuji." is a
        // legitimate message and must stay.
        var textToPaste = await _civilityTrimmer.TrimIfClaudeCodeAsync(result.Text, cancellationToken);
        if (string.IsNullOrWhiteSpace(textToPaste))
        {
            _logger.LogInformation("Quick dictation: transcription reduced to empty after civility trim — skipping paste");
            _outputChannel.StopTypingFeedback();
            _stateMachine.TransitionTo(DictationState.Idle);
            return;
        }

        // Quick mode: fast paste without clipboard save/restore.
        var pasteSucceeded = await _outputChannel.FastPasteAsync(textToPaste, cancellationToken);
        _outputChannel.StopTypingFeedback();

        if (!pasteSucceeded)
        {
            _logger.LogWarning("Quick dictation: fast paste failed");
            _stateMachine.TransitionTo(DictationState.Idle);
            return;
        }

        // Broadcast QuickTranscriptionCompleted BEFORE idle transition so the
        // client sets the pending flag first.
        await _outputChannel.BroadcastEventAsync(DictationEventType.QuickTranscriptionCompleted, textToPaste);
        _stateMachine.TransitionTo(DictationState.Idle);
        _logger.LogInformation("Quick dictation: text pasted, QuickTranscriptionCompleted sent");
    }

    public async Task CompleteFullAsync(
        byte[] audio,
        Action<string> onTranscriptionReady,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audio);
        ArgumentNullException.ThrowIfNull(onTranscriptionReady);

        _outputChannel.StartTypingFeedback();

        var result = await _transcriber.TranscribeFullAsync(audio, cancellationToken);

        if (!result.Success || string.IsNullOrWhiteSpace(result.Text))
        {
            _logger.LogWarning("Transcription failed or empty");
            _outputChannel.StopTypingFeedback();
            _stateMachine.TransitionTo(DictationState.Idle);
            return;
        }

        _logger.LogInformation("Transcription result: '{Text}'", result.Text);

        await _persister.SaveAsync(audio, result, cancellationToken);

        // Raise TranscriptionCompleted before typing so remote UI gets the text immediately.
        onTranscriptionReady(result.Text);

        var typed = await _outputChannel.TypeIntoActiveWindowAsync(result.Text, cancellationToken);
        _outputChannel.StopTypingFeedback();

        if (!typed)
        {
            _logger.LogWarning("Failed to type text into active window");
            _stateMachine.TransitionTo(DictationState.Idle);
            return;
        }

        _logger.LogInformation("Text typed successfully into active window");
        _stateMachine.TransitionTo(DictationState.Idle);
    }
}
