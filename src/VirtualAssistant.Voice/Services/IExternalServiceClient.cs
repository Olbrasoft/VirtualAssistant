using Olbrasoft.VirtualAssistant.Voice.Dtos;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Client for calling external services (PTT, task dispatch).
/// </summary>
public interface IExternalServiceClient
{
    /// <summary>
    /// Calls the PTT repeat endpoint to get the last text from history.
    /// </summary>
    Task<(bool Success, PttRepeatResponse? Response, string? Error)> CallPttRepeatAsync(CancellationToken ct);

    /// <summary>
    /// Dispatches a task to the specified agent.
    /// </summary>
    Task<(bool Success, VoiceDispatchTaskResponse? Response, string? Error)> DispatchTaskAsync(string targetAgent, CancellationToken ct);
}
