using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Thread-safe service for managing manual mute state.
/// Mute can be toggled via ScrollLock key or tray menu.
/// </summary>
public class ManualMuteService : IManualMuteService
{
    private readonly ILogger<ManualMuteService> _logger;
    private readonly object _lock = new();
    private bool _isMuted;

    public event EventHandler<bool>? MuteStateChanged;

    public ManualMuteService(ILogger<ManualMuteService> logger)
    {
        _logger = logger;
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
    public bool Toggle()
    {
        lock (_lock)
        {
            _isMuted = !_isMuted;
            _logger.LogInformation("Mute toggled: {State}", _isMuted ? "MUTED" : "UNMUTED");
            
            // Raise event outside lock would be better, but for simplicity
            MuteStateChanged?.Invoke(this, _isMuted);
            
            return _isMuted;
        }
    }

    /// <inheritdoc />
    public void SetMuted(bool muted)
    {
        lock (_lock)
        {
            if (_isMuted == muted) return;
            
            _isMuted = muted;
            _logger.LogInformation("Mute set: {State}", _isMuted ? "MUTED" : "UNMUTED");
            
            MuteStateChanged?.Invoke(this, _isMuted);
        }
    }
}
