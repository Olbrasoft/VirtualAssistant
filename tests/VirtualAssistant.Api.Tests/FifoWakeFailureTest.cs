namespace Olbrasoft.VirtualAssistant.Api.Tests;

/// <summary>
/// DELIBERATE FAILING TEST for FIFO wake test (issue #893).
/// Will be reverted immediately after verifying failedStep=test notification.
/// </summary>
public class FifoWakeFailureTest
{
    [Fact]
    public void Deliberate_failure_for_fifo_wake_test()
    {
        Assert.True(false, "Deliberate test failure for FIFO wake failedStep=test verification (issue #893)");
    }
}
