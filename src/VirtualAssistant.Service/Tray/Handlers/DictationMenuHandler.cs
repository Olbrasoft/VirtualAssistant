using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Tray.Handlers;

/// <inheritdoc />
public sealed class DictationMenuHandler : IDictationMenuHandler
{
    private readonly ILogger<DictationMenuHandler> _logger;
    private readonly IDictationControl? _dictationControl;

    public DictationMenuHandler(
        ILogger<DictationMenuHandler> logger,
        IDictationControl? dictationControl = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dictationControl = dictationControl;
    }

    public void HandleDictationToggle(bool enabled)
    {
        try
        {
            if (_dictationControl == null)
            {
                _logger.LogWarning("Dictation control not available");
                return;
            }

            _logger.LogInformation("Setting dictation enabled: {Enabled}", enabled);
            _dictationControl.SetDictationEnabled(enabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle dictation");
        }
    }

    public void HandleStreamingTranscriptionToggle(bool enabled)
    {
        try
        {
            if (_dictationControl == null)
            {
                _logger.LogWarning("Dictation control not available");
                return;
            }

            _logger.LogInformation("Setting streaming transcription enabled: {Enabled}", enabled);
            _dictationControl.SetStreamingTranscriptionEnabled(enabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle streaming transcription");
        }
    }
}
