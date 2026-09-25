using PingRunner.Core.Network;

namespace PingRunner.App.Tests.Fakes;

/// <summary>A home Wi-Fi connection with documentation-range addresses.</summary>
public sealed class FixedConnectionInfoSource(ConnectionSnapshot? snapshot) : IConnectionInfoSource
{
    public static ConnectionSnapshot HomeWiFi { get; } = new(
        "Wi-Fi",
        "Intel(R) Wi-Fi 6E AX211 160MHz",
        "Wi-Fi",
        1_201_000_000,
        ["192.168.1.24", "2001:db8:5:1::24"],
        ["192.168.1.1"],
        ["192.168.1.1", "1.1.1.1"]);

    public ConnectionSnapshot? GetPrimary() => snapshot;
}
