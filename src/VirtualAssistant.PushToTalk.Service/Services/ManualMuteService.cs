using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.PushToTalk.Service.Services;

/// <summary>
/// Implementation of manual mute service using internal state tracking.
/// ScrollLock LED doesn't work on Wayland, so we track state internally.
/// </summary>
public class ManualMuteService : IManualMuteService
{
    private readonly ILogger<ManualMuteService> _logger;
    private bool _isMuted;
    private readonly object _lock = new();
    
    public ManualMuteService(ILogger<ManualMuteService> logger)
    {
        _logger = logger;
        _isMuted = false;
    }
    
    /// <inheritdoc />
    public bool IsMuted
    {
        get
        {
            lock (_lock)
            {
                return _isMuted;
            }
        }
    }
    
    /// <inheritdoc />
    public event EventHandler<bool>? MuteStateChanged;
    
    /// <inheritdoc />
    public bool Toggle()
    {
        lock (_lock)
        {
            _isMuted = !_isMuted;
            _logger.LogInformation("Manual mute toggled: {State}", _isMuted ? "MUTED" : "UNMUTED");
            MuteStateChanged?.Invoke(this, _isMuted);
            return _isMuted;
        }
    }
    
    /// <inheritdoc />
    public void SetMuted(bool muted)
    {
        lock (_lock)
        {
            if (_isMuted != muted)
            {
                _isMuted = muted;
                _logger.LogInformation("Manual mute set to: {State}", _isMuted ? "MUTED" : "UNMUTED");
                MuteStateChanged?.Invoke(this, _isMuted);
            }
        }
    }
}
