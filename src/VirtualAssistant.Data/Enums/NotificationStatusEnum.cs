namespace VirtualAssistant.Data.Enums;

/// <summary>
/// Enum representing all possible notification statuses in the workflow.
/// Values match the Id in NotificationStatus reference table.
/// </summary>
public enum NotificationStatusEnum
{
    /// <summary>
    /// Notification just received from agent, waiting to be processed.
    /// </summary>
    NewlyReceived = 1,

    /// <summary>
    /// VirtualAssistant picked up the notification for processing.
    /// </summary>
    Processing = 2,

    /// <summary>
    /// Sent to LLM for summarization.
    /// </summary>
    SentForSummarization = 3,

    /// <summary>
    /// Returned from LLM, summarized.
    /// </summary>
    Summarized = 4,

    /// <summary>
    /// Sent for translation.
    /// </summary>
    SentForTranslation = 5,

    /// <summary>
    /// Returned translated.
    /// </summary>
    Translated = 6,

    /// <summary>
    /// User was notified about pending notifications (e.g., "You have 4 new messages").
    /// </summary>
    Announced = 7,

    /// <summary>
    /// Waiting for user to decide whether to play the notification.
    /// </summary>
    WaitingForPlayback = 8,

    /// <summary>
    /// Notification was played to the user.
    /// </summary>
    Played = 9
}
