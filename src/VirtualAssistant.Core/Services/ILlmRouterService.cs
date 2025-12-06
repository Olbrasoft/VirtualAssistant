using Olbrasoft.VirtualAssistant.Core.Enums;

namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Interface for LLM-based voice input routing services.
/// Implementations determine whether input should be sent to OpenCode, responded directly, 
/// executed as bash command, or ignored.
/// </summary>
public interface ILlmRouterService
{
    /// <summary>
    /// Name of the LLM provider (for logging).
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Routes voice input through the LLM and returns the routing decision.
    /// </summary>
    /// <param name="inputText">The transcribed voice input.</param>
    /// <param name="isDiscussionActive">Whether discussion mode is currently active.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Routing result with action, response, and timing information.</returns>
    Task<LlmRouterResult> RouteAsync(string inputText, bool isDiscussionActive = false, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result from LLM Router containing routing decision and associated data.
/// </summary>
public class LlmRouterResult
{
    /// <summary>
    /// Gets a value indicating whether the routing was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the routing action determined by the LLM.
    /// </summary>
    public LlmRouterAction Action { get; init; }

    /// <summary>
    /// Gets the confidence score of the routing decision (0.0 to 1.0).
    /// </summary>
    public float Confidence { get; init; }

    /// <summary>
    /// Gets the reason for the routing decision.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Gets the response text to speak via TTS (when Action is Respond).
    /// </summary>
    public string? Response { get; init; }

    /// <summary>
    /// Gets the command to send to OpenCode (when Action is OpenCode).
    /// </summary>
    public string? CommandForOpenCode { get; init; }

    /// <summary>
    /// Gets the bash command to execute (when Action is Bash).
    /// </summary>
    public string? BashCommand { get; init; }
    
    /// <summary>
    /// Gets the title for the note (when Action is SaveNote).
    /// </summary>
    public string? NoteTitle { get; init; }

    /// <summary>
    /// Gets the content for the note (when Action is SaveNote).
    /// </summary>
    public string? NoteContent { get; init; }

    /// <summary>
    /// Gets the topic of the discussion (when Action is StartDiscussion).
    /// </summary>
    public string? DiscussionTopic { get; init; }
    
    /// <summary>
    /// Gets the response time in milliseconds.
    /// </summary>
    public int ResponseTimeMs { get; init; }

    /// <summary>
    /// Gets the error message if routing failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// Type of prompt detected by the LLM router.
    /// Determines the mode (Build/Plan) and label prefix for OpenCode.
    /// </summary>
    public PromptType PromptType { get; init; } = PromptType.Command;

    /// <summary>
    /// Name of the LLM provider that processed this result.
    /// </summary>
    public string? ProviderName { get; init; }

    /// <summary>
    /// Creates a result for ignored input.
    /// </summary>
    /// <param name="reason">The reason for ignoring the input.</param>
    /// <returns>A new <see cref="LlmRouterResult"/> with Action set to Ignore.</returns>
    public static LlmRouterResult Ignored(string reason) => new()
    {
        Success = true,
        Action = LlmRouterAction.Ignore,
        Confidence = 1.0f,
        Reason = reason,
        ResponseTimeMs = 0
    };

    /// <summary>
    /// Creates a result for a routing error.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="responseTimeMs">The response time in milliseconds.</param>
    /// <returns>A new <see cref="LlmRouterResult"/> indicating failure.</returns>
    public static LlmRouterResult Error(string message, int responseTimeMs) => new()
    {
        Success = false,
        Action = LlmRouterAction.Ignore,
        ErrorMessage = message,
        ResponseTimeMs = responseTimeMs
    };
}
