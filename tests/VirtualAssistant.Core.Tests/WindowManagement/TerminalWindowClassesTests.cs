using Olbrasoft.VirtualAssistant.Core.WindowManagement;

namespace Olbrasoft.VirtualAssistant.Core.Tests.WindowManagement;

public class TerminalWindowClassesTests
{
    [Theory]
    [InlineData("OpenCode")]
    [InlineData("opencode")]
    [InlineData("kitty")]
    public void IsTerminal_KnownTerminalClass_ReturnsTrue(string wmClass)
    {
        Assert.True(TerminalWindowClasses.IsTerminal(wmClass));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("google-chrome")]
    public void IsTerminal_NonTerminalClass_ReturnsFalse(string? wmClass)
    {
        Assert.False(TerminalWindowClasses.IsTerminal(wmClass));
    }
}
