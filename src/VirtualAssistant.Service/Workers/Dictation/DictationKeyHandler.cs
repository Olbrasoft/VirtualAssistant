using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.StateMachine;

namespace Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

/// <inheritdoc />
public sealed class DictationKeyHandler : IDictationKeyHandler, IDisposable
{
    private readonly ILogger<DictationKeyHandler> _logger;
    private readonly IKeyboardMonitor _keyboardMonitor;
    private IDictationKeyHandlerBindings? _bindings;
    private bool _subscribed;

    public DictationKeyHandler(
        ILogger<DictationKeyHandler> logger,
        IKeyboardMonitor keyboardMonitor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _keyboardMonitor = keyboardMonitor ?? throw new ArgumentNullException(nameof(keyboardMonitor));
    }

    public void Start(IDictationKeyHandlerBindings bindings)
    {
        _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        if (_subscribed) return;
        _keyboardMonitor.KeyReleased += OnKeyReleased;
        _subscribed = true;
    }

    public void Stop()
    {
        if (_subscribed)
        {
            _keyboardMonitor.KeyReleased -= OnKeyReleased;
            _subscribed = false;
        }

        // Clear bindings so the handler doesn't pin the worker reference
        // past Stop — Copilot review on PR #1034.
        _bindings = null;
    }

    public void Dispose() => Stop();

    /// <summary>
    /// Key-release entry point. Uses fire-and-forget because the keyboard
    /// monitor's event handler signature is synchronous; the async body is
    /// exception-wrapped inside <see cref="HandleKeyReleasedAsync"/>.
    /// </summary>
    private void OnKeyReleased(object? sender, KeyEventArgs e) => _ = HandleKeyReleasedAsync(e);

    private async Task HandleKeyReleasedAsync(KeyEventArgs e)
    {
        var bindings = _bindings;
        if (bindings is null) return;

        try
        {
            // Globally gated — the enable flag short-circuits every key.
            if (!bindings.IsEnabled) return;

            // Pause: cancel recording or transcription depending on state.
            if (e.Key == KeyCode.Pause)
            {
                var state = bindings.State;

                if (state == DictationState.Recording)
                {
                    _logger.LogInformation("Pause pressed during recording - canceling dictation");
                    await bindings.CancelRecordingAsync();
                    return;
                }

                if (state == DictationState.Transcribing)
                {
                    _logger.LogInformation("Pause pressed - canceling transcription");
                    bindings.CancelTranscription();
                    return;
                }
            }

            // Only ScrollLock drives the start/stop toggle.
            if (e.Key != KeyCode.ScrollLock) return;

            var currentState = bindings.State;
            _logger.LogDebug("ScrollLock released - State: {State}", currentState);

            // Toggle logic: Idle → start, Recording → stop + transcribe,
            // Transcribing → ignored (use Pause to cancel).
            //
            // HandleKeyReleasedAsync is itself fire-and-forget from OnKeyReleased,
            // so awaiting directly here is safe: it keeps exceptions inside the
            // outer catch block instead of orphaning them on a detached Task.Run.
            // (Copilot review on PR #1034.)
            switch (currentState)
            {
                case DictationState.Idle:
                    _logger.LogInformation("ScrollLock pressed - starting dictation");
                    await bindings.StartAsync();
                    break;
                case DictationState.Recording:
                    _logger.LogInformation("ScrollLock pressed - stopping dictation");
                    await bindings.StopAndTranscribeAsync();
                    break;
                case DictationState.Transcribing:
                    _logger.LogDebug("ScrollLock pressed during transcription - ignored (use Pause to cancel)");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling key release");
        }
    }
}
