using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Enums;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.TextInput;
using Olbrasoft.VirtualAssistant.Voice.Audio;
using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using VirtualAssistant.Core.Services;
using KeyCode = Olbrasoft.VirtualAssistant.Core.Services.KeyCode;

namespace Olbrasoft.VirtualAssistant.Voice;

/// <summary>
/// Main worker that continuously listens for speech, transcribes it,
/// and uses LLM Router to determine actions (OpenCode, Respond, Ignore).
///
/// Includes TTS echo cancellation via AssistantSpeechTrackerService.
/// Also handles PTT history repeat requests via RepeatTextIntentService.
/// </summary>
public class ContinuousListenerWorker : BackgroundService
{
    private readonly ILogger<ContinuousListenerWorker> _logger;
    private readonly AudioCaptureService _audioCapture;
    private readonly VadService _vad;
    private readonly TranscriptionService _transcription;
    private readonly ILlmRouterService _llmRouter;
    private readonly TextInputService _textInput;
    private readonly ContinuousListenerOptions _options;
    private readonly IManualMuteService? _muteService;
    private readonly AssistantSpeechTrackerService _speechTracker;
    private readonly IRepeatTextIntentService _repeatTextIntent;
    private readonly IVirtualAssistantSpeaker _speaker;
    private readonly IKeyboardMonitor? _keyboardMonitor;

    // Extracted services
    private readonly IVoiceStateMachine _stateMachine;
    private readonly ISpeechBufferManager _bufferManager;
    private readonly ICommandDetectionService _commandDetection;
    private readonly IExternalServiceClient _externalService;

    // Transcription cancellation
    private CancellationTokenSource? _transcriptionCts;
    private bool _isTranscribing;

    public ContinuousListenerWorker(
        ILogger<ContinuousListenerWorker> logger,
        AudioCaptureService audioCapture,
        VadService vad,
        TranscriptionService transcription,
        ILlmRouterService llmRouter,
        TextInputService textInput,
        IOptions<ContinuousListenerOptions> options,
        AssistantSpeechTrackerService speechTracker,
        IRepeatTextIntentService repeatTextIntent,
        IVirtualAssistantSpeaker speaker,
        IVoiceStateMachine stateMachine,
        ISpeechBufferManager bufferManager,
        ICommandDetectionService commandDetection,
        IExternalServiceClient externalService,
        IManualMuteService? muteService = null,
        IKeyboardMonitor? keyboardMonitor = null)
    {
        _logger = logger;
        _audioCapture = audioCapture;
        _vad = vad;
        _transcription = transcription;
        _llmRouter = llmRouter;
        _textInput = textInput;
        _options = options.Value;
        _speechTracker = speechTracker;
        _repeatTextIntent = repeatTextIntent;
        _speaker = speaker;
        _stateMachine = stateMachine;
        _bufferManager = bufferManager;
        _commandDetection = commandDetection;
        _externalService = externalService;
        _muteService = muteService;
        _keyboardMonitor = keyboardMonitor;

        _logger.LogInformation("ContinuousListener starting in {State} state (StartMuted={StartMuted})",
            _stateMachine.CurrentState, _options.StartMuted);

        // Subscribe to mute state changes
        if (_muteService != null)
        {
            _muteService.MuteStateChanged += OnMuteStateChanged;
            _logger.LogDebug("Subscribed to MuteStateChanged event");
        }
        else
        {
            _logger.LogWarning("IManualMuteService is NULL - mute functionality disabled!");
        }

        // Subscribe to keyboard events for Escape key
        if (_keyboardMonitor != null)
        {
            _keyboardMonitor.KeyReleased += OnKeyReleased;
            _logger.LogDebug("Subscribed to keyboard events for transcription cancellation");
        }
    }

    private void OnKeyReleased(object? sender, KeyEventArgs e)
    {
        if (e.Key == KeyCode.Escape && _isTranscribing && _transcriptionCts != null)
        {
            _logger.LogInformation("Escape pressed - canceling transcription");
            _transcriptionCts.Cancel();
        }
    }

    private void OnMuteStateChanged(object? sender, bool isMuted)
    {
        try
        {
            _logger.LogInformation("Mute state changed: {IsMuted}", isMuted);

            if (isMuted)
            {
                if (_stateMachine.CurrentState == VoiceState.Recording)
                {
                    _logger.LogInformation("Muted during recording - cancelling");
                    _bufferManager.ClearAll();
                }
                _stateMachine.ResetToMuted();
                _audioCapture.Stop();
                _logger.LogInformation("Microphone released");
            }
            else
            {
                _audioCapture.Start();
                _stateMachine.TransitionTo(VoiceState.Waiting);
                _logger.LogInformation("Microphone started - listening resumed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnMuteStateChanged");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _transcription.Initialize();

            if (_muteService?.IsMuted == true)
            {
                _stateMachine.TransitionTo(VoiceState.Muted);
                _logger.LogInformation("ContinuousListener started - MUTED");
            }
            else
            {
                _audioCapture.Start();
                _logger.LogInformation("ContinuousListener started - listening for speech");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize services");
            return;
        }

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_muteService?.IsMuted == true)
                {
                    await Task.Delay(100, stoppingToken);
                    continue;
                }

                var chunk = await _audioCapture.ReadChunkAsync(stoppingToken);
                if (chunk == null)
                {
                    await Task.Delay(100, stoppingToken);
                    continue;
                }

                await ProcessChunkAsync(chunk, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not started"))
        {
            _logger.LogDebug("Audio capture stopped, waiting for unmute");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in main loop");
        }
        finally
        {
            _audioCapture.Stop();
            _logger.LogInformation("ContinuousListener stopped");
        }
    }

    private async Task ProcessChunkAsync(byte[] chunk, CancellationToken cancellationToken)
    {
        if (_muteService?.IsMuted == true)
        {
            _stateMachine.TransitionTo(VoiceState.Muted);
            return;
        }

        var (isSpeech, rms) = _vad.Analyze(chunk);
        var currentState = _stateMachine.CurrentState;

        switch (currentState)
        {
            case VoiceState.Muted:
                _stateMachine.TransitionTo(VoiceState.Waiting);
                goto case VoiceState.Waiting;

            case VoiceState.Waiting:
                if (isSpeech)
                {
                    _stateMachine.StartRecording(rms);
                    _bufferManager.TransferPreBufferToSpeech();
                    _bufferManager.AddToSpeechBuffer(chunk);
                }
                else
                {
                    _bufferManager.AddToPreBuffer(chunk);
                }
                break;

            case VoiceState.Recording:
                _bufferManager.AddToSpeechBuffer(chunk);

                if (isSpeech)
                {
                    _stateMachine.SilenceStartTime = default;
                }
                else
                {
                    if (_stateMachine.SilenceStartTime == default)
                    {
                        _stateMachine.SilenceStartTime = DateTime.UtcNow;
                    }

                    var silenceMs = (DateTime.UtcNow - _stateMachine.SilenceStartTime).TotalMilliseconds;
                    var recordingMs = (DateTime.UtcNow - _stateMachine.RecordingStartTime).TotalMilliseconds;

                    if (silenceMs >= _options.PostSilenceMs)
                    {
                        if (recordingMs >= _options.MinRecordingMs)
                        {
                            await CompleteRecordingAsync(cancellationToken);
                        }
                        else
                        {
                            ResetToWaiting();
                        }
                    }
                }
                break;
        }
    }

    private async Task CompleteRecordingAsync(CancellationToken cancellationToken)
    {
        var audioData = _bufferManager.GetCombinedSpeechData();
        _transcriptionCts = new CancellationTokenSource();
        _isTranscribing = true;

        try
        {
            _logger.LogDebug("Starting transcription...");
            var transcriptionResult = await _transcription.TranscribeAsync(audioData, _transcriptionCts.Token);

            if (!transcriptionResult.Success || string.IsNullOrWhiteSpace(transcriptionResult.Text))
            {
                return;
            }

            var text = transcriptionResult.Text;
            _logger.LogInformation("Transcribed: \"{Text}\"", text);

            // Filter out TTS echo
            var filteredText = _speechTracker.FilterEchoFromTranscription(text);
            if (string.IsNullOrWhiteSpace(filteredText))
            {
                _logger.LogDebug("Echo filtered - entire transcription was TTS output");
                ResetToWaiting();
                return;
            }

            if (filteredText != text)
            {
                _logger.LogDebug("Echo filtered: \"{FilteredText}\"", filteredText);
                text = filteredText;
            }

            // Cancel command
            if (_commandDetection.IsCancelCommand(text))
            {
                _logger.LogInformation("Cancel command detected - prompt cancelled");
                ResetToWaiting();
                return;
            }

            // Local pre-filter
            if (_commandDetection.ShouldSkipLocally(text))
            {
                _logger.LogDebug("Skipped locally (too short or noise)");
                ResetToWaiting();
                return;
            }

            // Stop command
            if (_commandDetection.IsStopCommand(text))
            {
                _logger.LogInformation("Stop command detected");
                ResetToWaiting();
                return;
            }

            // Check for repeat text intent
            _logger.LogDebug("Checking repeat text intent...");
            var repeatIntent = await _repeatTextIntent.DetectIntentAsync(text, cancellationToken);

            if (repeatIntent.IsRepeatTextIntent && repeatIntent.Confidence >= 0.7f)
            {
                _logger.LogInformation("Repeat text intent detected (confidence: {Confidence:F2})", repeatIntent.Confidence);
                await HandleRepeatTextAsync(cancellationToken);
                ResetToWaiting();
                return;
            }

            // Route through LLM
            _logger.LogInformation("Routing to LLM: \"{Text}\"", text);
            var routerResult = await _llmRouter.RouteAsync(text, false, cancellationToken);

            _logger.LogInformation("{Provider}: {Action} [{PromptType}] (confidence: {Confidence:F2}, {Time}ms)",
                _llmRouter.ProviderName, routerResult.Action, routerResult.PromptType,
                routerResult.Confidence, routerResult.ResponseTimeMs);

            if (!string.IsNullOrEmpty(routerResult.Reason))
            {
                _logger.LogDebug("Reason: {Reason}", routerResult.Reason);
            }

            await HandleRouterResultAsync(text, routerResult, cancellationToken);
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
            ResetToWaiting();
        }
    }

    private async Task HandleRouterResultAsync(string text, LlmRouterResult routerResult, CancellationToken cancellationToken)
    {
        switch (routerResult.Action)
        {
            case LlmRouterAction.OpenCode:
                await HandleOpenCodeActionAsync(text, routerResult.PromptType, cancellationToken);
                break;

            case LlmRouterAction.Respond:
                HandleRespondAction(routerResult.Response);
                break;

            case LlmRouterAction.SaveNote:
                _logger.LogInformation("Note saving not implemented");
                break;

            case LlmRouterAction.StartDiscussion:
            case LlmRouterAction.EndDiscussion:
                _logger.LogInformation("Discussion mode not implemented");
                break;

            case LlmRouterAction.DispatchTask:
                await HandleDispatchTaskActionAsync(routerResult.TargetAgent ?? "claude", cancellationToken);
                break;

            case LlmRouterAction.Ignore:
                // Already logged
                break;
        }
    }

    private async Task HandleOpenCodeActionAsync(string command, PromptType promptType, CancellationToken cancellationToken)
    {
        var agent = promptType switch
        {
            PromptType.Command => "build",
            PromptType.Confirmation => "build",
            PromptType.Continuation => "build",
            PromptType.Question => "plan",
            PromptType.Acknowledgement => "plan",
            _ => "plan"
        };

        _logger.LogInformation("Sending to OpenCode with agent: {Agent}", agent);
        var success = await _textInput.SendMessageToSessionAsync(command, agent, cancellationToken);

        if (success)
        {
            _logger.LogInformation("Message sent to OpenCode");
        }
        else
        {
            _logger.LogWarning("Failed to send message to OpenCode");
        }
    }

    private void HandleRespondAction(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            _logger.LogWarning("LLM returned RESPOND but no response text");
            return;
        }

        _logger.LogInformation("Response: \"{Response}\"", response);
    }

    private async Task HandleRepeatTextAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Calling PTT repeat endpoint");
        var (success, response, error) = await _externalService.CallPttRepeatAsync(cancellationToken);

        if (success && response != null)
        {
            var preview = response.Text?.Length > 50 ? response.Text[..50] + "..." : response.Text;
            _logger.LogInformation("Text copied to clipboard: \"{Text}\"", preview);
            var phrase = _repeatTextIntent.GetRandomClipboardResponse();
            await _speaker.SpeakAsync(phrase, agentName: null, ct: cancellationToken);
        }
        else if (error == "No text in history")
        {
            _logger.LogWarning("No text in history");
            await _speaker.SpeakAsync("Zadny text v historii.", agentName: null, ct: cancellationToken);
        }
        else
        {
            _logger.LogError("PTT repeat failed: {Error}", error);
            await _speaker.SpeakAsync("Nepodarilo se ziskat text.", agentName: null, ct: cancellationToken);
        }
    }

    private async Task HandleDispatchTaskActionAsync(string targetAgent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Dispatching task to {Agent}", targetAgent);
        var (success, response, error) = await _externalService.DispatchTaskAsync(targetAgent, cancellationToken);

        if (success && response?.Success == true)
        {
            var issueInfo = response.GithubIssueNumber.HasValue ? $" (issue #{response.GithubIssueNumber})" : "";
            _logger.LogInformation("Task dispatched to {Agent}{IssueInfo}", targetAgent, issueInfo);

            var ttsMessage = response.GithubIssueNumber.HasValue
                ? $"Posilam ukol cislo {response.GithubIssueNumber}."
                : "Ukol odeslan.";
            await _speaker.SpeakAsync(ttsMessage, agentName: null, ct: cancellationToken);
        }
        else if (response != null)
        {
            _logger.LogWarning("{Message}", response.Message);

            var ttsMessage = response.Reason switch
            {
                "agent_busy" => $"{targetAgent} je zaneprazdneny.",
                "no_pending_tasks" => "Zadne cekajici ukoly.",
                _ => response.Message ?? "Nepodarilo se odeslat ukol."
            };
            await _speaker.SpeakAsync(ttsMessage, agentName: null, ct: cancellationToken);
        }
        else
        {
            _logger.LogError("Dispatch failed: {Error}", error);
            await _speaker.SpeakAsync("Chyba pri odesilani ukolu.", agentName: null, ct: cancellationToken);
        }
    }

    private void ResetToWaiting()
    {
        _bufferManager.ClearSpeechBuffer();
        _stateMachine.ResetToWaiting();
        _logger.LogDebug("State reset to Waiting");
    }
}
