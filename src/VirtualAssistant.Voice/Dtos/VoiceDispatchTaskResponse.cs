namespace Olbrasoft.VirtualAssistant.Voice.Dtos;

/// <summary>
/// Response from dispatch task endpoint.
/// </summary>
public record VoiceDispatchTaskResponse(
    bool Success,
    string? Reason,
    string? Message,
    int? TaskId,
    int? GithubIssueNumber,
    string? GithubIssueUrl,
    string? Summary);
