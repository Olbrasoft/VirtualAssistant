using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Core.Tests.Services;

/// <summary>
/// The tilde-expansion helper is small but lives on a hot startup path
/// (ClaudeDispatchService reads the expanded path before every spawn) —
/// pin the edge cases so a future refactor doesn't break relative paths.
/// </summary>
public class ClaudeDispatchOptionsTests
{
    [Fact]
    public void GetExpandedWorkingDirectory_WithTildePrefix_ExpandsToHome()
    {
        var options = new ClaudeDispatchOptions { DefaultWorkingDirectory = "~/projects/foo" };
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var expanded = options.GetExpandedWorkingDirectory();

        Assert.Equal(Path.Combine(home, "projects/foo"), expanded);
    }

    [Fact]
    public void GetExpandedWorkingDirectory_WithAbsolutePath_ReturnsUnchanged()
    {
        var options = new ClaudeDispatchOptions { DefaultWorkingDirectory = "/opt/olbrasoft/virtual-assistant" };

        var expanded = options.GetExpandedWorkingDirectory();

        Assert.Equal("/opt/olbrasoft/virtual-assistant", expanded);
    }

    [Fact]
    public void GetExpandedWorkingDirectory_WithRelativePath_ReturnsUnchanged()
    {
        // Only a leading "~/" is expanded. Other relative paths are left as-is
        // so the caller can feed them straight into ProcessStartInfo.
        var options = new ClaudeDispatchOptions { DefaultWorkingDirectory = "foo/bar" };

        var expanded = options.GetExpandedWorkingDirectory();

        Assert.Equal("foo/bar", expanded);
    }

    [Fact]
    public void GetExpandedWorkingDirectory_WithBareTilde_ReturnsUnchanged()
    {
        // A bare "~" without the trailing "/" is NOT expanded. The expansion
        // rule is explicitly "~/" prefix only — this pins the edge case
        // Copilot on PR #1015 asked for so a future refactor doesn't silently
        // start expanding plain "~".
        var options = new ClaudeDispatchOptions { DefaultWorkingDirectory = "~" };

        var expanded = options.GetExpandedWorkingDirectory();

        Assert.Equal("~", expanded);
    }

    [Fact]
    public void Ctor_DefaultValues_MatchDocumentedContract()
    {
        // The default values are contract: ClaudeDispatchService caller sites
        // fall back to them when the options section is missing. Pin so a
        // silent change surfaces here instead of in a deploy drift. Renamed
        // to Member_Scenario_Expected per the project's naming convention
        // (Copilot review on PR #1015).
        var options = new ClaudeDispatchOptions();

        Assert.Equal(string.Empty, options.NotifyUrl);
        Assert.Equal("~/Olbrasoft/VirtualAssistant", options.DefaultWorkingDirectory);
        Assert.Equal(30, options.TimeoutMinutes);
        Assert.True(options.NotifyOnSuccess);
    }
}
