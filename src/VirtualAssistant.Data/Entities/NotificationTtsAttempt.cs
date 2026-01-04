namespace Olbrasoft.VirtualAssistant.Data.Entities;

/// <summary>
/// Records every TTS synthesis attempt for a notification (tracks fallback chain).
/// </summary>
public class NotificationTtsAttempt
{
    public int Id { get; set; }

    public int NotificationId { get; set; }
    public Notification Notification { get; set; } = null!;

    public int ProviderId { get; set; }
    public Provider Provider { get; set; } = null!;

    public int AttemptOrder { get; set; } // 1, 2, 3 (position in fallback chain)
    public required string StatusCode { get; set; } // "success", "http_error", "timeout", "circuit_open"
    public int? HttpStatusCode { get; set; } // 200, 500, 503
    public string? ErrorMessage { get; set; }
    public int? DurationMs { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
