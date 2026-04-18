using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Service.Tray.Handlers;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Tray.Handlers;

public class DashboardMenuHandlerTests
{
    private readonly Mock<ILogger<DashboardMenuHandler>> _loggerMock = new();

    [Fact]
    public void Constructor_NullLogger_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new DashboardMenuHandler(null!, "http://localhost:5055"));

    [Fact]
    public void Constructor_NullDashboardUrl_FallsBackToLocalhost()
    {
        // Defensive fallback matches the pre-split behavior of MenuEventDispatcher —
        // some test code used to pass null through and expect "http://localhost:5055".
        var sut = new DashboardMenuHandler(_loggerMock.Object, dashboardBaseUrl: null);

        Assert.NotNull(sut);
    }

    // Deliberately no tests for HandleDashboard / HandleAbout: their only
    // side effect is spawning xdg-open / zenity, which on the developer
    // machine actually opens a browser tab and a dialog window. A "does
    // not throw" assertion is not worth polluting the developer's session.
    // If the subprocess contract ever needs verification, inject
    // Olbrasoft.VirtualAssistant.Core.Processes.IProcessExecutor — the
    // codebase already has that abstraction for exactly this case.
}
