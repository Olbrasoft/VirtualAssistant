using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Enums;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.TextInput;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice;

/// <summary>
/// Main worker that continuously listens for speech, transcribes it,
/// and uses LLM Router to determine actions (OpenCode, Respond, Ignore).
/// 
/// Includes TTS echo cancellation via AssistantSpeechTrackerService.
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

    // State machine
    private enum State { Waiting, Recording, Muted }
    private State _state = State.Waiting;

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
        IManualMuteService? muteService = null)
    {
        _logger = logger;
        _audioCapture = audioCapture;
        _vad = vad;
        _transcription = transcription;
        _llmRouter = llmRouter;
        _textInput = textInput;
        _options = options.Value;
        _speechTracker = speechTracker;
        _muteService = muteService;

        // Subscribe to mute state changes
        if (_muteService != null)
        {
            _muteService.MuteStateChanged += OnMuteStateChanged;
        }
    }

    private void OnMuteStateChanged(object? sender, bool isMuted)
    {
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
        }
        else
        {
            Console.WriteLine("\u001b[92;1m🔊 UNMUTED - listening resumed\u001b[0m");
            _state = State.Waiting;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initialize services
        try
        {
            _transcription.Initialize();
            _audioCapture.Start();
            
            Console.WriteLine("\u001b[92;1m🎤 ContinuousListener started - listening for speech\u001b[0m");
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
                var chunk = await _audioCapture.ReadChunkAsync(stoppingToken);
                if (chunk == null) break;

                await ProcessChunkAsync(chunk, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
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

        try
        {
            // Transcribe speech
            var transcriptionResult = await _transcription.TranscribeAsync(audioData);

            if (!transcriptionResult.Success || string.IsNullOrWhiteSpace(transcriptionResult.Text))
            {
                ResetToWaiting();
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

            // White color for text being sent to LLM
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing speech");
        }
        finally
        {
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
