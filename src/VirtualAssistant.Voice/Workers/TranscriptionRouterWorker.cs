using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Enums;
using Olbrasoft.VirtualAssistant.Core.Events;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Workers;

/// <summary>
/// Worker responsible for transcription and LLM routing.
/// Subscribes to speech events, transcribes audio, routes to LLM.
/// Single Responsibility: STT + LLM routing.
/// </summary>
public class TranscriptionRouterWorker : BackgroundService
{
    private readonly ILogger<TranscriptionRouterWorker> _logger;
    private readonly IEventBus _eventBus;
    private readonly ITranscriptionService _transcription;
    private readonly ILlmRouterService _llmRouter;
    private readonly IAssistantSpeechTrackerService _speechTracker;
    private readonly ICommandDetectionService _commandDetection;
    private readonly IRepeatTextIntentService _repeatTextIntent;
    private readonly IDisposable _speechEndedSubscription;
    private readonly IDisposable _transcriptionCancelledSubscription;

    private CancellationTokenSource? _transcriptionCts;
    private bool _isTranscribing;

    public TranscriptionRouterWorker(
        ILogger<TranscriptionRouterWorker> logger,
        IEventBus eventBus,
        ITranscriptionService transcription,
        ILlmRouterService llmRouter,
        IAssistantSpeechTrackerService speechTracker,
        ICommandDetectionService commandDetection,
        IRepeatTextIntentService repeatTextIntent)
    {
        _logger = logger;
        _eventBus = eventBus;
        _transcription = transcription;
        _llmRouter = llmRouter;
        _speechTracker = speechTracker;
        _commandDetection = commandDetection;
        _repeatTextIntent = repeatTextIntent;

        // Subscribe to events
        _speechEndedSubscription = _eventBus.Subscribe<SpeechEndedEvent>(OnSpeechEnded);
        _transcriptionCancelledSubscription = _eventBus.Subscribe<TranscriptionCancelledEvent>(OnTranscriptionCancelled);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _transcription.Initialize();
            _logger.LogInformation("TranscriptionRouterWorker started");

            // Keep service alive while listening to events
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("TranscriptionRouterWorker stopped");
        }
    }

    private async Task OnSpeechEnded(SpeechEndedEvent @event, CancellationToken cancellationToken)
    {
        if (_isTranscribing)
        {
            _logger.LogWarning("Already transcribing, ignoring new speech");
            return;
        }

        _isTranscribing = true;
        _transcriptionCts = new CancellationTokenSource();

        try
        {
            _logger.LogInformation("Transcribing speech ({DurationMs}ms)", @event.DurationMs);

            var result = await _transcription.TranscribeAsync(@event.AudioBuffer, _transcriptionCts.Token);

            if (!result.Success)
            {
                _logger.LogError("Transcription failed: {Error}", result.ErrorMessage);
                return;
            }

            var text = result.Text;

            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Transcription returned empty text");
                return;
            }

            _logger.LogInformation("Transcribed: \"{Text}\"", text);

            await _eventBus.PublishAsync(new TranscriptionCompletedEvent(text, @event.DurationMs), cancellationToken);

            // Echo cancellation check
            var filteredText = _speechTracker.FilterEchoFromTranscription(text);
            if (string.IsNullOrWhiteSpace(filteredText))
            {
                _logger.LogInformation("Echo detected, ignoring own speech");
                return;
            }

            // Command detection - stop command
            if (_commandDetection.IsStopCommand(filteredText))
            {
                _logger.LogInformation("Stop command detected");
                return;
            }

            // Command detection - repeat text intent
            _logger.LogDebug("Checking repeat text intent...");
            var repeatIntent = await _repeatTextIntent.DetectIntentAsync(filteredText, cancellationToken);

            if (repeatIntent.IsRepeatTextIntent && repeatIntent.Confidence >= 0.7f)
            {
                _logger.LogInformation("Repeat text intent detected (confidence: {Confidence:F2})", repeatIntent.Confidence);
                await _eventBus.PublishAsync(new ActionRequestedEvent(
                    LlmRouterAction.DispatchTask,
                    filteredText,
                    TargetAgent: "repeat-text"), cancellationToken);
                return;
            }

            // LLM Routing
            _logger.LogDebug("Routing transcription to LLM");
            var routerResult = await _llmRouter.RouteAsync(filteredText, false, _transcriptionCts.Token);

            await _eventBus.PublishAsync(new ActionRequestedEvent(
                routerResult.Action,
                text,
                routerResult.Response,
                routerResult.PromptType,
                routerResult.TargetAgent), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Transcription cancelled by user");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing speech");
        }
        finally
        {
            _isTranscribing = false;
            _transcriptionCts?.Dispose();
            _transcriptionCts = null;
        }
    }

    private Task OnTranscriptionCancelled(TranscriptionCancelledEvent @event, CancellationToken cancellationToken)
    {
        if (_isTranscribing && _transcriptionCts != null)
        {
            _logger.LogInformation("Canceling transcription");
            _transcriptionCts.Cancel();
        }
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _speechEndedSubscription?.Dispose();
        _transcriptionCancelledSubscription?.Dispose();
        _transcriptionCts?.Dispose();
        base.Dispose();
    }
}
