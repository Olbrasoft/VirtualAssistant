using System.Net.Http.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Enums;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.TextInput;
using Olbrasoft.VirtualAssistant.Voice.Audio;
using Olbrasoft.VirtualAssistant.Voice.Services;
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
    private readonly HttpClient _httpClient;
    private readonly IKeyboardMonitor? _keyboardMonitor;

    // PTT Service endpoint for repeat text
    private const string PttRepeatEndpoint = "http://localhost:5050/api/ptt/repeat";

    // State machine
    private enum State { Waiting, Recording, Muted }
    private State _state; // Initialized in constructor based on StartMuted config

    // Transcription cancellation
    private CancellationTokenSource? _transcriptionCts;
    private bool _isTranscribing;

    // Buffers
    private readonly Queue<byte[]> _preBuffer = new();
    private readonly List<byte[]> _speechBuffer = new();
    private int _preBufferBytes = 0;
    private int _speechBufferBytes = 0;

    // Timing
    private DateTime _silenceStart;
    private DateTime _recordingStart;
    private int _segmentCount = 0;

    // Stop command - immediately stops current action
    private static readonly HashSet<string> StopCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "stop"
    };

    // Cancel command - user wants to abort current prompt before sending to LLM
    private static readonly HashSet<string> CancelCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "cancel",
        "kencel"  // Possible Whisper transcription variant
    };



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
        HttpClient httpClient,
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
        _httpClient = httpClient;
        _muteService = muteService;
        _keyboardMonitor = keyboardMonitor;

        // Initialize state based on StartMuted configuration (#70)
        _state = _options.StartMuted ? State.Muted : State.Waiting;
        _logger.LogInformation("ContinuousListener starting in {State} state (StartMuted={StartMuted})",
            _state, _options.StartMuted);

        // Subscribe to mute state changes
        if (_muteService != null)
        {
            _muteService.MuteStateChanged += OnMuteStateChanged;
            _logger.LogWarning("Subscribed to MuteStateChanged event on instance {HashCode}", _muteService.GetHashCode());
        }
        else
        {
            _logger.LogWarning("IManualMuteService is NULL - mute functionality disabled!");
        }

        // Subscribe to keyboard events for Escape key
        if (_keyboardMonitor != null)
        {
            _keyboardMonitor.KeyReleased += OnKeyReleased;
            _logger.LogInformation("Subscribed to keyboard events for transcription cancellation");
        }
    }

    private void OnKeyReleased(object? sender, KeyEventArgs e)
    {
        // Handle Escape - cancel ongoing transcription
        if (e.Key == KeyCode.Escape)
        {
            if (_isTranscribing && _transcriptionCts != null)
            {
                _logger.LogInformation("Escape pressed - canceling transcription");
                _transcriptionCts.Cancel();
            }
        }
    }

    private void OnMuteStateChanged(object? sender, bool isMuted)
    {
        try
        {
            _logger.LogWarning("OnMuteStateChanged called: isMuted={IsMuted}", isMuted);
            
            if (isMuted)
            {
                // If we were recording, abort and go to muted state
                if (_state == State.Recording)
                {
                    Console.WriteLine("\u001b[93;1m🔇 MUTED - recording cancelled\u001b[0m");
                    ResetToMuted();
                }
                else
                {
                    Console.WriteLine("\u001b[93;1m🔇 MUTED\u001b[0m");
                    _state = State.Muted;
                }
                
                // Release microphone completely so other apps can use it
                _logger.LogWarning("About to release microphone...");
                _audioCapture.Stop();
                _logger.LogWarning("Microphone released successfully");
            }
            else
            {
                // Restart audio capture when unmuted
                _logger.LogWarning("About to start microphone...");
                _audioCapture.Start();
                _logger.LogWarning("Microphone started successfully");
                Console.WriteLine("\u001b[92;1m🔊 UNMUTED - listening resumed\u001b[0m");
                _state = State.Waiting;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnMuteStateChanged");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initialize services
        try
        {
            _transcription.Initialize();
            
            // Only start audio capture if not muted
            if (_muteService?.IsMuted == true)
            {
                _state = State.Muted;
                Console.WriteLine("\u001b[93;1m🎤 ContinuousListener started - MUTED (microphone not captured)\u001b[0m");
            }
            else
            {
                _audioCapture.Start();
                Console.WriteLine("\u001b[92;1m🎤 ContinuousListener started - listening for speech\u001b[0m");
            }
            _logger.LogInformation("ContinuousListener started");
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
                // Wait while muted - microphone is released
                if (_muteService?.IsMuted == true)
                {
                    await Task.Delay(100, stoppingToken);
                    continue;
                }
                
                var chunk = await _audioCapture.ReadChunkAsync(stoppingToken);
                if (chunk == null)
                {
                    // End of stream - might happen when audio capture is stopped
                    // Wait a bit and check if we should restart
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
            // Audio capture was stopped (muted) - this is expected, loop will restart
            _logger.LogDebug("Audio capture stopped, waiting for unmute");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in main loop");
        }
        finally
        {
            _audioCapture.Stop();
            Console.WriteLine("\u001b[93;1m🛑 ContinuousListener stopped\u001b[0m");
            _logger.LogInformation("ContinuousListener stopped");
        }
    }

    private async Task ProcessChunkAsync(byte[] chunk, CancellationToken cancellationToken)
    {
        // Skip processing if muted
        if (_muteService?.IsMuted == true)
        {
            _state = State.Muted;
            return;
        }

        var (isSpeech, rms) = _vad.Analyze(chunk);

        switch (_state)
        {
            case State.Muted:
                // Was muted but now unmuted - transition to waiting
                _state = State.Waiting;
                goto case State.Waiting;

            case State.Waiting:
                if (isSpeech)
                {
                    TransitionToRecording(rms);
                    
                    // Move pre-buffer to speech buffer
                    while (_preBuffer.Count > 0)
                    {
                        var c = _preBuffer.Dequeue();
                        _speechBuffer.Add(c);
                        _speechBufferBytes += c.Length;
                    }
                    _preBufferBytes = 0;

                    // Add current chunk
                    _speechBuffer.Add(chunk);
                    _speechBufferBytes += chunk.Length;
                }
                else
                {
                    // Keep in pre-buffer
                    _preBuffer.Enqueue(chunk);
                    _preBufferBytes += chunk.Length;

                    // Trim if too large
                    while (_preBufferBytes > _options.PreBufferMaxBytes && _preBuffer.Count > 0)
                    {
                        var removed = _preBuffer.Dequeue();
                        _preBufferBytes -= removed.Length;
                    }
                }
                break;

            case State.Recording:
                // Always add to speech buffer
                _speechBuffer.Add(chunk);
                _speechBufferBytes += chunk.Length;

                if (isSpeech)
                {
                    // Reset silence timer
                    _silenceStart = default;
                }
                else
                {
                    // Start or continue silence timer
                    if (_silenceStart == default)
                    {
                        _silenceStart = DateTime.UtcNow;
                    }

                    var silenceMs = (DateTime.UtcNow - _silenceStart).TotalMilliseconds;
                    var recordingMs = (DateTime.UtcNow - _recordingStart).TotalMilliseconds;

                    // Check if we should complete
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

    private void TransitionToRecording(float probability)
    {
        _state = State.Recording;
        _recordingStart = DateTime.UtcNow;
        _silenceStart = default;
        
        Console.WriteLine($"\u001b[94m🔴 RECORDING (VAD: {probability:F2})\u001b[0m");
    }

    private async Task CompleteRecordingAsync(CancellationToken cancellationToken)
    {
        _segmentCount++;
        var duration = DateTime.UtcNow - _recordingStart;

        // Combine all chunks
        var audioData = new byte[_speechBufferBytes];
        int offset = 0;
        foreach (var chunk in _speechBuffer)
        {
            Buffer.BlockCopy(chunk, 0, audioData, offset, chunk.Length);
            offset += chunk.Length;
        }

        // Create cancellation token for transcription (can be cancelled with Escape key)
        _transcriptionCts = new CancellationTokenSource();
        _isTranscribing = true;

        try
        {
            // Transcribe speech
            _logger.LogDebug("Starting transcription... (press Escape to cancel)");
            var transcriptionResult = await _transcription.TranscribeAsync(audioData, _transcriptionCts.Token);

            if (!transcriptionResult.Success || string.IsNullOrWhiteSpace(transcriptionResult.Text))
            {
                return;
            }

            var text = transcriptionResult.Text;
            
            // Bright cyan for transcript from Whisper
            Console.WriteLine($"\u001b[96;1m📝 \"{text}\"\u001b[0m");

            // Filter out TTS echo (assistant's own speech captured by microphone)
            var filteredText = _speechTracker.FilterEchoFromTranscription(text);
            if (string.IsNullOrWhiteSpace(filteredText))
            {
                Console.WriteLine($"\u001b[95m🔇 Echo filtered - entire transcription was TTS output\u001b[0m");
                ResetToWaiting();
                return;
            }
            
            if (filteredText != text)
            {
                Console.WriteLine($"\u001b[95m🔇 Echo filtered: \"{filteredText}\"\u001b[0m");
                text = filteredText;
            }

            // Cancel command - user wants to abort this prompt before sending to LLM
            if (IsCancelCommand(text))
            {
                Console.WriteLine($"\u001b[93;1m✋ CANCEL - prompt cancelled, nothing sent\u001b[0m");
                ResetToWaiting();
                return;
            }

            // Local pre-filter: skip short/noise phrases before calling LLM API
            if (ShouldSkipLocally(text))
            {
                Console.WriteLine($"\u001b[90m⏭️ Skipped locally (too short or noise)\u001b[0m");
                ResetToWaiting();
                return;
            }

            // Check for STOP command
            if (IsStopCommand(text))
            {
                Console.WriteLine($"\u001b[91;1m🛑 STOP command detected\u001b[0m");
                // In simplified version, just acknowledge and continue
                ResetToWaiting();
                return;
            }

            // FIRST LLM QUERY: Check for repeat text intent (PTT history feature)
            // This is done before the main router to handle "vrať mi text" requests
            Console.WriteLine($"\u001b[90m🔍 Checking repeat text intent...\u001b[0m");
            var repeatIntent = await _repeatTextIntent.DetectIntentAsync(text, cancellationToken);
            
            if (repeatIntent.IsRepeatTextIntent && repeatIntent.Confidence >= 0.7f)
            {
                Console.WriteLine($"\u001b[95;1m📋 REPEAT TEXT INTENT detected (confidence: {repeatIntent.Confidence:F2}, {repeatIntent.ResponseTimeMs}ms)\u001b[0m");
                Console.WriteLine($"\u001b[95;1m   └─ {repeatIntent.Reason}\u001b[0m");
                
                // Call PTT repeat endpoint
                await HandleRepeatTextAsync(cancellationToken);
                ResetToWaiting();
                return;
            }
            else
            {
                Console.WriteLine($"\u001b[90m   └─ Not repeat intent ({repeatIntent.ResponseTimeMs}ms): {repeatIntent.Reason}\u001b[0m");
            }

            // SECOND LLM QUERY: Normal routing through multi-provider LLM
            Console.WriteLine($"\u001b[97;1m🤖 → LLM: \"{text}\"\u001b[0m");

            // Route through LLM
            var routerResult = await _llmRouter.RouteAsync(text, false, cancellationToken);

            // Colored output for LLM router decision
            var actionColor = routerResult.Action == LlmRouterAction.Ignore 
                ? "\u001b[91;1m"  // Red for IGNORE
                : "\u001b[92;1m"; // Green for actions
            var promptTypeIndicator = routerResult.PromptType switch
            {
                PromptType.Command => "⚡",
                PromptType.Confirmation => "✓",
                PromptType.Continuation => "→",
                PromptType.Question => "❓",
                PromptType.Acknowledgement => "📝",
                _ => "?"
            };
            Console.WriteLine($"{actionColor}🎯 {_llmRouter.ProviderName}: {routerResult.Action.ToString().ToUpper()} {promptTypeIndicator} [{routerResult.PromptType}] (confidence: {routerResult.Confidence:F2}, {routerResult.ResponseTimeMs}ms)\u001b[0m");
            
            if (!string.IsNullOrEmpty(routerResult.Reason))
            {
                Console.WriteLine($"{actionColor}   └─ {routerResult.Reason}\u001b[0m");
            }

            switch (routerResult.Action)
            {
                case LlmRouterAction.OpenCode:
                    await HandleOpenCodeActionAsync(text, routerResult.PromptType, cancellationToken);
                    break;

                case LlmRouterAction.Respond:
                    await HandleRespondActionAsync(routerResult.Response, cancellationToken);
                    break;

                case LlmRouterAction.SaveNote:
                    Console.WriteLine($"\u001b[95;1m📓 Note saving not implemented in this version\u001b[0m");
                    break;

                case LlmRouterAction.StartDiscussion:
                case LlmRouterAction.EndDiscussion:
                    Console.WriteLine($"\u001b[96;1m💬 Discussion mode not implemented in this version\u001b[0m");
                    break;

                case LlmRouterAction.Ignore:
                    // Already logged above
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Transcription cancelled by user (Escape key pressed)");
            Console.WriteLine($"\u001b[93;1m✋ CANCELLED - transcription cancelled by Escape key\u001b[0m");
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

    private async Task HandleOpenCodeActionAsync(string command, PromptType promptType, CancellationToken cancellationToken)
    {
        // Determine agent based on prompt type
        var agent = promptType switch
        {
            PromptType.Command => "build",
            PromptType.Confirmation => "build",
            PromptType.Continuation => "build",
            PromptType.Question => "plan",
            PromptType.Acknowledgement => "plan",
            _ => "plan" // Default to plan (safer)
        };
        
        var agentColor = agent == "plan" ? "\u001b[96;1m" : "\u001b[93;1m";
        var agentIcon = agent == "plan" ? "❓" : "⚡";
        
        Console.WriteLine($"{agentColor}{agentIcon} Sending to OpenCode with agent: {agent}\u001b[0m");
        
        var success = await _textInput.SendMessageToSessionAsync(command, agent, cancellationToken);
        
        if (success)
        {
            Console.WriteLine($"\u001b[92;1m✓ Message sent to OpenCode\u001b[0m");
        }
        else
        {
            Console.WriteLine($"\u001b[91;1m✗ Failed to send message to OpenCode\u001b[0m");
        }
    }

    private Task HandleRespondActionAsync(string? response, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            _logger.LogWarning("LLM returned RESPOND but no response text");
            return Task.CompletedTask;
        }

        // In simplified version, just log the response (no TTS)
        Console.WriteLine($"\u001b[92;1m🔊 Response: \"{response}\"\u001b[0m");
        Console.WriteLine($"\u001b[90m   (TTS not implemented in this version)\u001b[0m");
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles repeat text request - calls PTT service and speaks confirmation.
    /// </summary>
    private async Task HandleRepeatTextAsync(CancellationToken cancellationToken)
    {
        try
        {
            Console.WriteLine($"\u001b[95;1m📋 Calling PTT repeat endpoint...\u001b[0m");
            
            var response = await _httpClient.PostAsync(PttRepeatEndpoint, null, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<PttRepeatResponse>(cancellationToken: cancellationToken);
                
                Console.WriteLine($"\u001b[92;1m✓ Text copied to clipboard: \"{result?.Text?.Substring(0, Math.Min(50, result?.Text?.Length ?? 0))}...\"\u001b[0m");
                
                // TTS confirmation with random phrase (Issue #68, #115)
                var phrase = _repeatTextIntent.GetRandomClipboardResponse();
                await _speaker.SpeakAsync(phrase, agentName: null, cancellationToken);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.WriteLine($"\u001b[93;1m⚠ No text in history\u001b[0m");
                await _speaker.SpeakAsync("Žádný text v historii.", agentName: null, cancellationToken);
            }
            else
            {
                Console.WriteLine($"\u001b[91;1m✗ PTT repeat failed: {response.StatusCode}\u001b[0m");
                await _speaker.SpeakAsync("Nepodařilo se získat text.", agentName: null, cancellationToken);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to call PTT repeat endpoint");
            Console.WriteLine($"\u001b[91;1m✗ PTT service unavailable: {ex.Message}\u001b[0m");
            await _speaker.SpeakAsync("Služba není dostupná.", agentName: null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling repeat text request");
            Console.WriteLine($"\u001b[91;1m✗ Error: {ex.Message}\u001b[0m");
        }
    }

    /// <summary>
    /// Response from PTT repeat endpoint.
    /// </summary>
    private record PttRepeatResponse(string? Text, string? Message);

    private void ResetToWaiting()
    {
        _state = State.Waiting;
        _speechBuffer.Clear();
        _speechBufferBytes = 0;
        _silenceStart = default;

        Console.WriteLine("\u001b[90m→ WAITING\u001b[0m");
    }

    private void ResetToMuted()
    {
        _state = State.Muted;
        _speechBuffer.Clear();
        _speechBufferBytes = 0;
        _silenceStart = default;
        _preBuffer.Clear();
        _preBufferBytes = 0;
    }

    /// <summary>
    /// Local pre-filter to skip noise phrases before calling LLM API.
    /// </summary>
    private bool ShouldSkipLocally(string text)
    {
        var normalized = text.Trim().ToLowerInvariant();
        
        var noisePatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ano", "ne", "jo", "no", "tak", "hmm", "hm", "aha", "ok", "okay",
            "dobře", "jasně", "fajn", "super", "díky", "děkuji", "prosím",
            "moment", "počkej", "ehm", "ehm ehm", "no tak", "tak jo",
            "to je", "to bylo", "a tak", "no jo", "no ne", "tak tak",
            "jasně jasně", "jo jo", "ne ne", "aha aha", "mm", "mhm",
            "no nic", "nic", "nevím", "uvidíme", "možná", "asi",
            "co to", "co je", "hele", "hele hele", "víš co", "že jo",
            "no jasně", "no dobře", "no fajn", "to jo", "to ne",
            "tak nějak", "nějak", "prostě", "vlastně", "takže",
            "...", ".", ",", "!", "?"
        };

        return noisePatterns.Contains(normalized.TrimEnd('.', ',', '!', '?', ' '));
    }

    /// <summary>
    /// Checks if the text contains a stop command.
    /// </summary>
    internal static bool IsStopCommand(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
            
        var normalized = text.Trim().ToLowerInvariant();
        var words = normalized.Split(new[] { ' ', ',', '.', '!', '?', ';', ':' }, 
            StringSplitOptions.RemoveEmptyEntries);
            
        foreach (var word in words)
        {
            if (StopCommands.Contains(word))
                return true;
        }
        
        return false;
    }

    /// <summary>
    /// Checks if the text contains a cancel command.
    /// </summary>
    internal static bool IsCancelCommand(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
            
        var normalized = text.Trim().ToLowerInvariant();
        var words = normalized.Split(new[] { ' ', ',', '.', '!', '?', ';', ':' }, 
            StringSplitOptions.RemoveEmptyEntries);
            
        foreach (var word in words)
        {
            if (CancelCommands.Contains(word))
                return true;
        }
        
        return false;
    }
}
