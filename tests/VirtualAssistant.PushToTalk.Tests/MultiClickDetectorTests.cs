using Olbrasoft.VirtualAssistant.PushToTalk;

namespace VirtualAssistant.PushToTalk.Tests;

public class MultiClickDetectorTests
{
    [Fact]
    public void Constructor_WithDefaults_SetsDefaultValues()
    {
        // Arrange & Act
        using var detector = new MultiClickDetector("TEST");

        // Assert
        Assert.Equal(MultiClickDetector.DefaultClickThresholdMs, detector.ClickThresholdMs);
        Assert.Equal(MultiClickDetector.DefaultClickDebounceMs, detector.ClickDebounceMs);
        Assert.Equal(3, detector.MaxClickCount);
    }

    [Fact]
    public void Constructor_WithCustomMaxClickCount_SetsValue()
    {
        // Arrange & Act
        using var detector = new MultiClickDetector("TEST", maxClickCount: 2);

        // Assert
        Assert.Equal(2, detector.MaxClickCount);
    }

    [Fact]
    public async Task RegisterClick_TripleClick_FiresTripleClickEvent()
    {
        // Arrange
        using var detector = new MultiClickDetector("TEST", maxClickCount: 3);
        var result = ClickResult.Pending;
        var eventFired = new TaskCompletionSource<ClickResult>();

        detector.ClickDetected += (_, e) =>
        {
            result = e.Result;
            eventFired.TrySetResult(e.Result);
        };

        // Act - Three rapid clicks
        detector.RegisterClick();
        await Task.Delay(100);
        detector.RegisterClick();
        await Task.Delay(100);
        detector.RegisterClick();

        // Assert - Triple click should fire immediately
        var clickResult = await eventFired.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(ClickResult.TripleClick, clickResult);
    }

    [Fact]
    public async Task RegisterClick_SingleClick_FiresSingleClickAfterTimeout()
    {
        // Arrange
        using var detector = new MultiClickDetector("TEST", maxClickCount: 3);
        detector.ClickThresholdMs = 200; // Short threshold for faster test

        var eventFired = new TaskCompletionSource<ClickResult>();
        detector.ClickDetected += (_, e) => eventFired.TrySetResult(e.Result);

        // Act - Single click
        detector.RegisterClick();

        // Assert - Should fire after timeout
        var clickResult = await eventFired.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(ClickResult.SingleClick, clickResult);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        // Arrange
        using var detector = new MultiClickDetector("TEST");
        detector.RegisterClick();

        // Act
        detector.Reset();

        // Assert - No exception means success
        detector.RegisterClick(); // Should start fresh sequence
    }

    [Fact]
    public void Dispose_PreventsSubsequentRegistrations()
    {
        // Arrange
        var detector = new MultiClickDetector("TEST");
        detector.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => detector.RegisterClick());
    }
}
