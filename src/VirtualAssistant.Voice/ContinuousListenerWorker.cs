using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Enums;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Voice.Audio;
using Olbrasoft.VirtualAssistant.Voice.Pipeline;
using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using KeyCode = Olbrasoft.VirtualAssistant.Core.Services.KeyCode;

namespace Olbrasoft.VirtualAssistant.Voice;

/// <summary>
/// Main worker that continuously listens for speech and processes it through a pipeline.
/// Uses Pipeline pattern to reduce complexity and improve testability.
/// </summary>
public class ContinuousListenerWorker : BackgroundService
{
    private readonly ILogger<ContinuousListenerWorker> _logger;
    private readonly AudioCaptureService _audioCapture;
    private readonly VadService _vad;
    private readonly IVoicePipeline _pipeline;
    private readonly ContinuousListenerOptions _options;
    private readonly IManualMuteService? _muteService;
    private readonly IKeyboardMonitor? _keyboardMonitor;

    // Extracted services
    private readonly IVoiceStateMachine _stateMachine;
    private readonly ISpeechBufferManager _bufferManager;

    // Transcription cancellation
    private CancellationTokenSource? _transcriptionCts;
    private bool _isTranscribing;

    public ContinuousListenerWorker(
        ILogger<ContinuousListenerWorker> logger,
        AudioCaptureService audioCapture,
        VadService vad,
        IVoicePipeline pipeline,
        IOptions<ContinuousListenerOptions> options,
        IVoiceStateMachine stateMachine,
        ISpeechBufferManager bufferManager,
        IManualMuteService? muteService = null,
        IKeyboardMonitor? keyboardMonitor = null)
    {
        _logger = logger;
        _audioCapture = audioCapture;
        _vad = vad;
        _pipeline = pipeline;
        _options = options.Value;
        _stateMachine = stateMachine;
        _bufferManager = bufferManager;
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
            _logger.LogDebug("Starting voice pipeline...");

            // Process audio through the complete pipeline
            await _pipeline.ProcessAsync(audioData, _transcriptionCts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Pipeline cancelled by user");
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

    private void ResetToWaiting()
    {
        _bufferManager.ClearSpeechBuffer();
        _stateMachine.ResetToWaiting();
        _logger.LogDebug("State reset to Waiting");
    }
}
