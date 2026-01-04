namespace Olbrasoft.VirtualAssistant.Data.Entities;

/// <summary>
/// Represents an external service provider (TTS, LLM, etc.).
/// </summary>
public class Provider
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; } // "tts", "llm", etc.
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<NotificationTtsAttempt> TtsAttempts { get; set; } = new List<NotificationTtsAttempt>();
}
