// THIS TEST IS INTENTIONALLY FAILING.
// It exists ONLY to verify that the FIFO push-wake mechanism delivers a
// CI failure event back to the originating Claude Code session.
// The PR containing this file MUST NOT be merged — close it after the
// failure wake event has been received.

using Xunit;

namespace VirtualAssistant.Voice.Tests;

public class IntentionalNegativeTestForCiWake
{
    [Fact]
    public void CiNegativeWakeTest_AlwaysFails()
    {
        // Force a failure to trigger a CI failure wake event.
        Assert.True(false, "Intentional failure for CI wake testing");
    }
}
