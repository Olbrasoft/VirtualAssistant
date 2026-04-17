using Olbrasoft.VirtualAssistant.Core.WindowManagement;

namespace Olbrasoft.VirtualAssistant.Core.Tests.WindowManagement;

/// <summary>
/// Cache-freshness contract for <see cref="TerminalCliAppDetector"/>. The full
/// <c>DetectCliAppAsync</c> flow shells out to gdbus/pgrep and is tested manually;
/// here we pin down just the pure time-based freshness predicate that decides
/// whether a transient gdbus failure may be papered over with a cached value
/// instead of falling back to Ctrl+V (which Claude Code hijacks as "paste image").
/// </summary>
public class TerminalCliAppDetectorCacheTests
{
    [Fact]
    public void IsCacheFresh_WithUninitializedCache_ReturnsFalse()
    {
        // Detector has never completed a successful probe yet, so the cache must
        // not be served — the "DateTime.MinValue" sentinel means "no cache".
        var result = TerminalCliAppDetector.IsCacheFresh(DateTime.MinValue, DateTime.UtcNow);

        Assert.False(result);
    }

    [Fact]
    public void IsCacheFresh_OneSecondOld_ReturnsTrue()
    {
        var now = DateTime.UtcNow;
        var cachedAt = now.AddSeconds(-1);

        var result = TerminalCliAppDetector.IsCacheFresh(cachedAt, now);

        Assert.True(result);
    }

    [Fact]
    public void IsCacheFresh_ExactlyAtStalenessBoundary_ReturnsTrue()
    {
        // Boundary is inclusive (<=), so a cache entry right at the limit still
        // serves. This prevents a tiny clock drift from flipping us to "stale".
        var now = DateTime.UtcNow;
        var cachedAt = now - TerminalCliAppDetector.CacheStaleness;

        var result = TerminalCliAppDetector.IsCacheFresh(cachedAt, now);

        Assert.True(result);
    }

    [Fact]
    public void IsCacheFresh_OneSecondPastStaleness_ReturnsFalse()
    {
        var now = DateTime.UtcNow;
        var cachedAt = now - TerminalCliAppDetector.CacheStaleness - TimeSpan.FromSeconds(1);

        var result = TerminalCliAppDetector.IsCacheFresh(cachedAt, now);

        Assert.False(result);
    }
}
