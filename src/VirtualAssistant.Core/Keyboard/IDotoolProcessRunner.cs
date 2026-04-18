namespace Olbrasoft.VirtualAssistant.Core.Keyboard;

/// <summary>
/// Spawns a dotool process, writes one or more <c>key {shortcut}</c> commands
/// to its stdin, and waits for exit with an independent timeout. Centralises
/// the process-juggling boilerplate that used to be duplicated across every
/// paste/send-key path in <see cref="XDoToolKeyboardService"/>.
/// </summary>
public interface IDotoolProcessRunner
{
    /// <summary>
    /// Sends one or more dotool <c>key</c> commands. Timeout is independent of
    /// <paramref name="cancellationToken"/> — a caller cancellation surfaces as
    /// <see cref="OperationCanceledException"/>, a timer expiry surfaces as
    /// <see cref="DotoolResult.TimedOut"/>.
    /// </summary>
    Task<DotoolResult> SendKeysAsync(
        IReadOnlyList<string> keys,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

/// <summary>
/// Outcome of a <see cref="IDotoolProcessRunner.SendKeysAsync"/> call.
/// </summary>
public readonly record struct DotoolResult(bool Success, string? Error, bool TimedOut)
{
    public static DotoolResult Ok() => new(true, null, false);
    public static DotoolResult Failed(string error) => new(false, error, false);
    public static DotoolResult Timeout() => new(false, null, true);
}
