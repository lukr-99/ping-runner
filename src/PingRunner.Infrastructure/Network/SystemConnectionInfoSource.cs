using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using PingRunner.Core.Network;

namespace PingRunner.Infrastructure.Network;

/// <summary>
/// Reads the adapters from the operating system and picks the one carrying the default route: up, not
/// loopback or a tunnel, with a gateway; an IPv4 gateway and then the faster link win a tie.
/// </summary>
public sealed class SystemConnectionInfoSource : IConnectionInfoSource
{
    public ConnectionSnapshot? GetPrimary()
    {
        try
        {
            return ReadPrimary();
        }
        catch (NetworkInformationException)
        {
            // Windows could not list its adapters; show the offline state rather than fail.
            return null;
        }
    }

    private static ConnectionSnapshot? ReadPrimary()
    {
        var primary = NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up
                && adapter.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .Select(adapter => (Adapter: adapter, Properties: adapter.GetIPProperties()))
            .Select(entry => (entry.Adapter, entry.Properties, Gateways: UsableGateways(entry.Properties)))
            .Where(entry => entry.Gateways.Count > 0)
            .OrderByDescending(entry => entry.Gateways.Any(gateway => gateway.AddressFamily == AddressFamily.InterNetwork))
            .ThenByDescending(entry => entry.Adapter.Speed)
            .FirstOrDefault();

        if (primary.Adapter is null)
        {
            return null;
        }

        return new ConnectionSnapshot(
            primary.Adapter.Name,
            primary.Adapter.Description,
            KindOf(primary.Adapter.NetworkInterfaceType),
            primary.Adapter.Speed > 0 ? primary.Adapter.Speed : null,
            [.. primary.Properties.UnicastAddresses
                .Select(unicast => unicast.Address)
                .Where(address => !address.IsIPv6LinkLocal)
                .OrderBy(address => address.AddressFamily == AddressFamily.InterNetworkV6)
                .Select(address => address.ToString())],
            [.. primary.Gateways.Select(gateway => gateway.ToString())],
            [.. primary.Properties.DnsAddresses
                .Where(address => !address.IsIPv6SiteLocal)
                .OrderBy(address => address.AddressFamily == AddressFamily.InterNetworkV6)
                .Select(address => address.ToString())]);
    }

    private static List<IPAddress> UsableGateways(IPInterfaceProperties properties) =>
    [
        .. properties.GatewayAddresses
            .Select(gateway => gateway.Address)
            .Where(address => !address.Equals(IPAddress.Any) && !address.Equals(IPAddress.IPv6Any))
            .OrderBy(address => address.AddressFamily == AddressFamily.InterNetworkV6),
    ];

    private static string KindOf(NetworkInterfaceType type) => type switch
    {
        NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetT
            or NetworkInterfaceType.FastEthernetFx or NetworkInterfaceType.Ethernet3Megabit => "Ethernet",
        NetworkInterfaceType.Wireless80211 => "Wi-Fi",
        NetworkInterfaceType.Wwanpp or NetworkInterfaceType.Wwanpp2 => "Mobile broadband",
        NetworkInterfaceType.Ppp => "Point-to-point (VPN or dial-up)",
        _ => type.ToString(),
    };
}
