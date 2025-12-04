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
/// Result from LLM Router.
/// </summary>
public class LlmRouterResult
{
    public bool Success { get; init; }
    public LlmRouterAction Action { get; init; }
    public float Confidence { get; init; }
    public string? Reason { get; init; }
    public string? Response { get; init; }
    public string? CommandForOpenCode { get; init; }
    public string? BashCommand { get; init; }
    
    /// <summary>
    /// Title for the note (when Action is SaveNote).
    /// </summary>
    public string? NoteTitle { get; init; }
    
    /// <summary>
    /// Content for the note (when Action is SaveNote).
    /// </summary>
    public string? NoteContent { get; init; }
    
    /// <summary>
    /// Topic of the discussion (when Action is StartDiscussion).
    /// </summary>
    public string? DiscussionTopic { get; init; }
    
    public int ResponseTimeMs { get; init; }
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

    public static LlmRouterResult Ignored(string reason) => new()
    {
        Success = true,
        Action = LlmRouterAction.Ignore,
        Confidence = 1.0f,
        Reason = reason,
        ResponseTimeMs = 0
    };

    public static LlmRouterResult Error(string message, int responseTimeMs) => new()
    {
        Success = false,
        Action = LlmRouterAction.Ignore,
        ErrorMessage = message,
        ResponseTimeMs = responseTimeMs
    };
}
