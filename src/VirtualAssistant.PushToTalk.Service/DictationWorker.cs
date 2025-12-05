using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.PushToTalk.Service.Services;
using Olbrasoft.VirtualAssistant.PushToTalk.TextInput;

namespace Olbrasoft.VirtualAssistant.PushToTalk.Service;

/// <summary>
/// Background worker service for push-to-talk dictation.
/// Monitors keyboard for CapsLock state changes and controls audio recording.
/// Records when CapsLock is ON, stops and transcribes when CapsLock is OFF.
/// </summary>
public class DictationWorker : BackgroundService
{
    /// <summary>
    /// Known Whisper hallucinations to filter out.
    /// Whisper model sometimes hallucinates common phrases when given silent or very short audio.
    /// </summary>
    private static readonly HashSet<string> WhisperHallucinations = new(StringComparer.OrdinalIgnoreCase)
    {
        "titulky vytvořil johnyx",
        "titulky vytvořil johny x",
        "titulky vytvořil johnny x",
        "titulky vytvořil johnyx.",
        "subtitles by johnyx",
        "subtitles created by johnyx",
        "thank you for watching",
        "thanks for watching"
    };

    private readonly ILogger<DictationWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IKeyboardMonitor _keyboardMonitor;
    private readonly IAudioRecorder _audioRecorder;
    private readonly ISpeechTranscriber _speechTranscriber;
    private readonly ITextTyper _textTyper;
    private readonly IPttNotifier _pttNotifier;
    private readonly HttpClient _httpClient;
    private readonly ManualMuteService _manualMuteService;
    private readonly ITranscriptionHistory _transcriptionHistory;
    private bool _isRecording;
    private bool _isTranscribing;
    private DateTime? _recordingStartTime;
    private KeyCode _triggerKey;
    private readonly string _ttsBaseUrl;
    private readonly string _virtualAssistantBaseUrl;

    /// <summary>
    /// Stores the mute state of VirtualAssistant before CapsLock recording started.
    /// Used to restore the original state after recording ends.
    /// </summary>
    private bool? _previousMuteState;

    /// <summary>
    /// CancellationTokenSource for the current transcription operation.
    /// Allows canceling Whisper transcription when user presses Escape.
    /// </summary>
    private CancellationTokenSource? _transcriptionCts;
    
    /// <summary>
    /// Path to the speech lock file. When this file exists, TTS should not speak.
    /// </summary>
    private const string SpeechLockFilePath = "/tmp/speech-lock";

    public DictationWorker(
        ILogger<DictationWorker> logger,
        IConfiguration configuration,
        IKeyboardMonitor keyboardMonitor,
        IAudioRecorder audioRecorder,
        ISpeechTranscriber speechTranscriber,
        ITextTyper textTyper,
        IPttNotifier pttNotifier,
        HttpClient httpClient,
        ManualMuteService manualMuteService,
        ITranscriptionHistory transcriptionHistory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _keyboardMonitor = keyboardMonitor ?? throw new ArgumentNullException(nameof(keyboardMonitor));
        _audioRecorder = audioRecorder ?? throw new ArgumentNullException(nameof(audioRecorder));
        _speechTranscriber = speechTranscriber ?? throw new ArgumentNullException(nameof(speechTranscriber));
        _textTyper = textTyper ?? throw new ArgumentNullException(nameof(textTyper));
        _pttNotifier = pttNotifier ?? throw new ArgumentNullException(nameof(pttNotifier));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _manualMuteService = manualMuteService ?? throw new ArgumentNullException(nameof(manualMuteService));
        _transcriptionHistory = transcriptionHistory ?? throw new ArgumentNullException(nameof(transcriptionHistory));

        // Load configuration
        var triggerKeyName = _configuration.GetValue<string>("PushToTalkDictation:TriggerKey", "CapsLock");
        _triggerKey = Enum.Parse<KeyCode>(triggerKeyName);
        _ttsBaseUrl = _configuration.GetValue<string>("EdgeTts:BaseUrl", "http://localhost:5555");
        _virtualAssistantBaseUrl = _configuration.GetValue<string>("VirtualAssistant:BaseUrl", "http://localhost:5055");

        _logger.LogWarning("=== NOTIFIER HASH: {Hash} ===", _pttNotifier.GetHashCode());
        _logger.LogInformation("Dictation worker initialized. Trigger key: {TriggerKey}", _triggerKey);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Push-to-Talk Dictation Service starting...");

        try
        {
            // Subscribe to keyboard events
            _keyboardMonitor.KeyPressed += OnKeyPressed;
            _keyboardMonitor.KeyReleased += OnKeyReleased;

            _logger.LogInformation("Press {TriggerKey} to start dictation, release to stop", _triggerKey);

            // Start keyboard monitoring (doesn't block)
            await _keyboardMonitor.StartMonitoringAsync(stoppingToken);

            // Wait indefinitely until cancellation is requested
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Dictation service is stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in dictation worker");
            throw;
        }
        finally
        {
            _keyboardMonitor.KeyPressed -= OnKeyPressed;
            _keyboardMonitor.KeyReleased -= OnKeyReleased;

            if (_isRecording)
            {
                await StopRecordingAsync();
            }
        }
    }

    private void OnKeyPressed(object? sender, KeyEventArgs e)
    {
        // We only care about the trigger key
        // Actual action is taken in OnKeyReleased after LED state updates
    }

    /// <summary>
    /// Handles ScrollLock (ManualMute) key press - toggles mute state and broadcasts to all clients.
    /// Uses internal state tracking since ScrollLock LED doesn't work on Wayland.
    /// </summary>
    private async Task HandleScrollLockPressedAsync()
    {
        try
        {
            // Toggle internal mute state (LED doesn't work on Wayland)
            var newMuteState = _manualMuteService.Toggle();
            
            _logger.LogInformation("ScrollLock pressed - ManualMute: {State}", 
                newMuteState ? "MUTED" : "UNMUTED");
            
            // Broadcast to all clients
            await _pttNotifier.NotifyManualMuteChangedAsync(newMuteState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle ScrollLock press");
        }
    }

    private void OnKeyReleased(object? sender, KeyEventArgs e)
    {
        // Handle ScrollLock (ManualMute) - toggle on key release
        if (e.Key == KeyCode.ScrollLock)
        {
            _ = Task.Run(async () => await HandleScrollLockPressedAsync());
            return;
        }

        // Handle Escape - cancel ongoing transcription
        if (e.Key == KeyCode.Escape)
        {
            if (_isTranscribing && _transcriptionCts != null)
            {
                _logger.LogInformation("Escape pressed - canceling transcription");
                _transcriptionCts.Cancel();
            }
            return;
        }

        if (e.Key != _triggerKey)
            return;

        // Read actual CapsLock state from LED - this is the reliable source of truth
        // LED state is updated by the kernel AFTER the key event is processed
        // Small delay to ensure LED state is updated
        Thread.Sleep(50);
        
        var capsLockOn = _keyboardMonitor.IsCapsLockOn();
        _logger.LogDebug("CapsLock released - LED state: {CapsLockOn}, Recording state: {Recording}", capsLockOn, _isRecording);

        if (capsLockOn && !_isRecording)
        {
            // CapsLock is ON and not recording - start recording
            _logger.LogInformation("CapsLock ON - starting dictation");
            _ = Task.Run(async () => await StartRecordingAsync());
        }
        else if (!capsLockOn && _isRecording)
        {
            // CapsLock is OFF and recording - stop and transcribe
            _logger.LogInformation("CapsLock OFF - stopping dictation");
            _ = Task.Run(async () => await StopRecordingAsync());
        }
        else
        {
            _logger.LogDebug("CapsLock state ({CapsLockOn}) matches recording state ({Recording}) - no action needed", 
                capsLockOn, _isRecording);
        }
    }

    private async Task StartRecordingAsync()
    {
        if (_isRecording)
        {
            _logger.LogWarning("Recording is already in progress");
            return;
        }

        try
        {
            _isRecording = true;
            _recordingStartTime = DateTime.UtcNow;

            _logger.LogInformation("Starting audio recording...");
            
            // CRITICAL: Stop TTS and create lock SYNCHRONOUSLY before anything else
            // This ensures TTS is stopped immediately when CapsLock is pressed
            // Note: EdgeTTS also polls CapsLock LED every 100ms as a backup
            
            // Stop any TTS speech immediately (fire-and-forget but don't await)
            _ = StopTtsSpeechAsync();
            
            // Save current mute state before forcing mute (to restore after recording)
            _previousMuteState = await GetVirtualAssistantMuteStateAsync();
            _logger.LogDebug("Saved previous mute state: {PreviousMuteState}", _previousMuteState);
            
            // Mute VirtualAssistant (changes tray icon to muted state)
            // Only set if not already muted
            if (_previousMuteState != true)
            {
                await SetVirtualAssistantMuteAsync(true);
            }
            
            // Create speech lock synchronously to prevent TTS from speaking during recording
            CreateSpeechLock();
            
            // Notify clients about recording start
            await _pttNotifier.NotifyRecordingStartedAsync();

            // Start recording (runs until cancelled)
            var cts = new CancellationTokenSource();
            await _audioRecorder.StartRecordingAsync(cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start recording");
            _isRecording = false;
            _recordingStartTime = null;
        }
    }

    private async Task StopRecordingAsync()
    {
        if (!_isRecording)
        {
            _logger.LogWarning("No recording in progress");
            return;
        }

        double durationSeconds = 0;
        
        try
        {
            _logger.LogInformation("Stopping audio recording...");

            await _audioRecorder.StopRecordingAsync();

            var recordedData = _audioRecorder.GetRecordedData();
            _logger.LogInformation("Recording stopped. Captured {ByteCount} bytes", recordedData.Length);

            // Calculate duration
            if (_recordingStartTime.HasValue)
            {
                durationSeconds = (DateTime.UtcNow - _recordingStartTime.Value).TotalSeconds;
                _logger.LogInformation("Total recording duration: {Duration:F2}s", durationSeconds);
            }
            
            // Notify clients about recording stop
            await _pttNotifier.NotifyRecordingStoppedAsync(durationSeconds);

            if (recordedData.Length > 0)
            {
                // Show icon and play sound IMMEDIATELY (before Whisper processing)
                await _pttNotifier.NotifyTranscriptionStartedAsync();

                // Create cancellation token for transcription (can be cancelled with Escape key)
                _transcriptionCts = new CancellationTokenSource();
                _isTranscribing = true;

                try
                {
                    _logger.LogInformation("Starting transcription... (press Escape to cancel)");
                    var transcription = await _speechTranscriber.TranscribeAsync(recordedData, _transcriptionCts.Token);

                    if (transcription.Success && !string.IsNullOrWhiteSpace(transcription.Text))
                {
                    // CRITICAL: Validate transcription to filter out Whisper hallucinations
                    if (!IsValidTranscription(transcription.Text))
                    {
                        // Hallucination detected - hide icon, stop sound, but DON'T type text
                        _logger.LogInformation("Whisper hallucination detected and filtered: '{Text}'", transcription.Text);
                        // Stop icon/sound by sending failed notification
                        await _pttNotifier.NotifyTranscriptionFailedAsync("Whisper hallucination filtered");
                        return; // STOP HERE - no text typing
                    }

                    _logger.LogInformation("Transcription successful: {Text} (confidence: {Confidence:F3})",
                        transcription.Text, transcription.Confidence);

                    // Notify clients about successful transcription
                    await _pttNotifier.NotifyTranscriptionCompletedAsync(transcription.Text, transcription.Confidence);

                    // Save to history before typing (allows repeat if pasted to wrong window)
                    _transcriptionHistory.SaveText(transcription.Text);
                    _logger.LogDebug("Transcription saved to history");

                    // Type transcribed text
                    await _textTyper.TypeTextAsync(transcription.Text);
                    _logger.LogInformation("Text typed successfully");
                }
                else
                {
                    var errorMessage = transcription.ErrorMessage ?? "Empty transcription result";
                    _logger.LogWarning("Transcription failed or empty: {Error}", errorMessage);

                    // Notify clients about failed transcription
                    await _pttNotifier.NotifyTranscriptionFailedAsync(errorMessage);
                }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Transcription cancelled by user (Escape key pressed)");
                    await _pttNotifier.NotifyTranscriptionFailedAsync("Transcription cancelled");
                }
                finally
                {
                    _isTranscribing = false;
                    _transcriptionCts?.Dispose();
                    _transcriptionCts = null;
                }
            }
            else
            {
                _logger.LogWarning("No audio data recorded");
                await _pttNotifier.NotifyTranscriptionFailedAsync("No audio data recorded");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop recording");
            await _pttNotifier.NotifyTranscriptionFailedAsync(ex.Message);
        }
        finally
        {
            _isRecording = false;
            _recordingStartTime = null;
            
            // Delete speech lock to allow TTS to speak again
            DeleteSpeechLock();
            
            // Restore VirtualAssistant mute state to what it was before recording
            // If it was muted before (via ScrollLock/menu), keep it muted
            // If it was unmuted before, unmute it now
            if (_previousMuteState.HasValue)
            {
                _logger.LogDebug("Restoring previous mute state: {PreviousMuteState}", _previousMuteState.Value);
                await SetVirtualAssistantMuteAsync(_previousMuteState.Value);
                _previousMuteState = null;
            }
            else
            {
                // Fallback: if we couldn't get previous state, unmute
                await SetVirtualAssistantMuteAsync(false);
            }
            
            // Flush any queued TTS messages that accumulated during dictation
            await FlushTtsQueueAsync();
        }
    }
    
    /// <summary>
    /// Validates transcription text to filter out Whisper hallucinations and empty results.
    /// </summary>
    /// <param name="text">Transcription text to validate</param>
    /// <returns>True if transcription is valid, false if it's a hallucination or invalid</returns>
    private bool IsValidTranscription(string? text)
    {
        // Empty or whitespace
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogDebug("Transcription validation failed: empty or whitespace");
            return false;
        }

        var trimmedText = text.Trim();

        // Too short (likely noise)
        if (trimmedText.Length < 2)
        {
            _logger.LogDebug("Transcription validation failed: too short ({Length} chars)", trimmedText.Length);
            return false;
        }

        // Known Whisper hallucinations
        if (WhisperHallucinations.Contains(trimmedText))
        {
            _logger.LogInformation("Whisper hallucination detected and filtered: '{Text}'", trimmedText);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Creates the speech lock file to signal TTS to not speak.
    /// </summary>
    private void CreateSpeechLock()
    {
        try
        {
            File.WriteAllText(SpeechLockFilePath, "PushToTalk:Recording");
            
            // Verify the file was actually created
            if (File.Exists(SpeechLockFilePath))
            {
                _logger.LogInformation("🔒 Speech lock file CREATED: {Path}", SpeechLockFilePath);
            }
            else
            {
                _logger.LogError("❌ Speech lock file NOT created despite no exception: {Path}", SpeechLockFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to create speech lock file: {Path}", SpeechLockFilePath);
        }
    }
    
    /// <summary>
    /// Deletes the speech lock file to allow TTS to speak again.
    /// </summary>
    private void DeleteSpeechLock()
    {
        try
        {
            if (File.Exists(SpeechLockFilePath))
            {
                File.Delete(SpeechLockFilePath);
                _logger.LogInformation("🔓 Speech lock file DELETED: {Path}", SpeechLockFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete speech lock file");
        }
    }

    /// <summary>
    /// Stops any currently playing TTS speech by calling the EdgeTTS server.
    /// </summary>
    private async Task StopTtsSpeechAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync($"{_ttsBaseUrl}/api/speech/stop", null);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("TTS speech stop request sent successfully");
            }
            else
            {
                _logger.LogWarning("TTS speech stop request failed: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send TTS speech stop request");
        }
    }

    /// <summary>
    /// Flushes the TTS message queue by calling VirtualAssistant service.
    /// Messages that were queued during dictation will be played.
    /// </summary>
    private async Task FlushTtsQueueAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync($"{_virtualAssistantBaseUrl}/api/tts/flush-queue", null);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("TTS queue flush request sent successfully");
            }
            else
            {
                _logger.LogWarning("TTS queue flush request failed: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send TTS queue flush request");
        }
    }

    /// <summary>
    /// Sets the mute state of VirtualAssistant service.
    /// When muted, the tray icon changes to indicate muted state.
    /// </summary>
    private async Task SetVirtualAssistantMuteAsync(bool muted)
    {
        try
        {
            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(new { Muted = muted }),
                System.Text.Encoding.UTF8,
                "application/json");
            
            var response = await _httpClient.PostAsync($"{_virtualAssistantBaseUrl}/api/mute", content);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("VirtualAssistant mute state set to: {Muted}", muted);
            }
            else
            {
                _logger.LogWarning("VirtualAssistant mute request failed: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set VirtualAssistant mute state to {Muted}", muted);
        }
    }

    /// <summary>
    /// Gets the current mute state of VirtualAssistant service.
    /// </summary>
    private async Task<bool?> GetVirtualAssistantMuteStateAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_virtualAssistantBaseUrl}/api/mute");
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("muted", out var mutedElement))
                {
                    return mutedElement.GetBoolean();
                }
            }
            else
            {
                _logger.LogWarning("VirtualAssistant mute state request failed: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get VirtualAssistant mute state");
        }
        
        return null;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Dictation service stopping...");

        if (_isRecording)
        {
            await StopRecordingAsync();
        }

        await _keyboardMonitor.StopMonitoringAsync();

        await base.StopAsync(cancellationToken);
    }
}
