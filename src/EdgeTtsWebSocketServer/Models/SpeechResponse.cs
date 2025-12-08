namespace Olbrasoft.VoiceAssistant.EdgeTtsWebSocketServer.Models;

public class SpeechResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool Cached { get; set; }
}
