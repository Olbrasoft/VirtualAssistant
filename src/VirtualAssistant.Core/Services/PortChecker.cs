using System.Net;
using System.Net.NetworkInformation;

namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Checks if network ports are available.
/// </summary>
public class PortChecker : IPortChecker
{
    /// <inheritdoc/>
    public bool IsPortAvailable(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var port = uri.Port;
        if (port == -1)
            port = uri.Scheme == "https" ? 443 : 80;

        var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
        var tcpConnections = ipGlobalProperties.GetActiveTcpConnections();

        return !tcpConnections.Any(conn =>
            conn.LocalEndPoint.Port == port &&
            conn.State == TcpState.Listen);
    }
}
