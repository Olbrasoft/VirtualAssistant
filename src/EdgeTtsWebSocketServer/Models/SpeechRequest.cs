namespace Olbrasoft.VoiceAssistant.EdgeTtsWebSocketServer.Models;

public class SpeechRequest
{
    public string Text { get; set; } = string.Empty;
    public string? Voice { get; set; }
    public string? Rate { get; set; }
    public string? Volume { get; set; }
    public string? Pitch { get; set; }
    public bool Play { get; set; } = true;
}
