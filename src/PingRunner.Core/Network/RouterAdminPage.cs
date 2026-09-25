using System.Net;
using System.Net.Sockets;

namespace PingRunner.Core.Network;

/// <summary>
/// The address a browser opens for a router's admin page: plain HTTP to the gateway's IP, which is
/// what home routers answer on (most redirect to HTTPS themselves). Only a parsed IP address becomes a
/// URL, so nothing else can turn this into an arbitrary link.
/// </summary>
public static class RouterAdminPage
{
    /// <summary>Null when <paramref name="gateway"/> is not an IP address.</summary>
    public static Uri? UrlFor(string? gateway)
    {
        if (!IPAddress.TryParse(gateway, out var address))
        {
            return null;
        }

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => new Uri($"http://{address}/"),

            // Browsers do not take an IPv6 zone (%12), so the address goes without it.
            AddressFamily.InterNetworkV6 => new Uri($"http://[{new IPAddress(address.GetAddressBytes())}]/"),
            _ => null,
        };
    }
}
