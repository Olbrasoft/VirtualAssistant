using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.WindowManagement;

/// <summary>
/// Short-TTL cache for the most recent successful CLI-app detection.
/// When a later probe fails transiently (gdbus slow, extension stalled),
/// the orchestrator serves this value instead of falling through to the
/// wrong paste shortcut. Claude Code intercepts Ctrl+V / Ctrl+Shift+V as
/// "paste image", so a Ctrl+V fallback triggers the "No image found in
/// clipboard" toast — the bug this cache exists to prevent. See issue #958.
/// </summary>
public sealed class CliAppDetectionCache
{
    /// <summary>
    /// Long enough to ride out a gdbus hiccup between consecutive taps of
    /// "Vložit rychle", short enough that a real app switch masked by a
    /// persistent gdbus outage flips us back to "no cached app" quickly.
    /// </summary>
    internal static readonly TimeSpan CacheStaleness = TimeSpan.FromSeconds(10);

    private readonly ILogger<CliAppDetectionCache>? _logger;
    private readonly object _lock = new();
    private CliAppDetectionResult? _result;
    private DateTime _cachedAtUtc = DateTime.MinValue;

    public CliAppDetectionCache(ILogger<CliAppDetectionCache>? logger = null)
    {
        _logger = logger;
    }

    public void Set(CliAppDetectionResult result)
    {
        lock (_lock)
        {
            _result = result;
            _cachedAtUtc = DateTime.UtcNow;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _result = null;
            _cachedAtUtc = DateTime.MinValue;
        }
    }

    /// <summary>
    /// Returns the cached value if it is still fresh, otherwise null.
    /// <paramref name="failureReason"/> is logged for the fresh-hit case so the
    /// caller's reason for asking ends up in the logs alongside the hit.
    /// </summary>
    public CliAppDetectionResult? TryGet(string failureReason)
    {
        CliAppDetectionResult? cached;
        DateTime cachedAt;
        lock (_lock)
        {
            cached = _result;
            cachedAt = _cachedAtUtc;
        }

        var now = DateTime.UtcNow;
        if (cached is not null && IsCacheFresh(cachedAt, now))
        {
            var age = now - cachedAt;
            _logger?.LogInformation(
                "Serving cached CLI app detection {AppName} (age {AgeSeconds:F1}s, reason: {Reason})",
                cached.AppName, age.TotalSeconds, failureReason);
            return cached;
        }

        _logger?.LogDebug("No usable cache (reason: {Reason})", failureReason);
        return null;
    }

    public static bool IsCacheFresh(DateTime cachedAtUtc, DateTime nowUtc)
        => cachedAtUtc != DateTime.MinValue && nowUtc - cachedAtUtc <= CacheStaleness;
}
