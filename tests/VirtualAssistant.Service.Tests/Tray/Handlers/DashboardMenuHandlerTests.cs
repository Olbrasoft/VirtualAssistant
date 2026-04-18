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
        var sut = new DashboardMenuHandler(_loggerMock.Object, null!);

        Assert.NotNull(sut);
    }

    [Fact]
    public void HandleDashboard_DoesNotThrow()
    {
        // The method spawns xdg-open; we can't meaningfully assert on the
        // subprocess, but exceptions must be swallowed on CI runners where
        // xdg-open is absent.
        var sut = new DashboardMenuHandler(_loggerMock.Object, "http://localhost:5055");

        var ex = Record.Exception(() => sut.HandleDashboard());

        Assert.Null(ex);
    }

    [Fact]
    public void HandleAbout_DoesNotThrow()
    {
        // Same reasoning as HandleDashboard — zenity may be absent; the
        // handler must log and return rather than propagate.
        var sut = new DashboardMenuHandler(_loggerMock.Object, "http://localhost:5055");

        var ex = Record.Exception(() => sut.HandleAbout());

        Assert.Null(ex);
    }
}
