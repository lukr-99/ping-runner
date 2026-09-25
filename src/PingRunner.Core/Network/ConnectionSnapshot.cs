namespace PingRunner.Core.Network;

/// <summary>
/// The network adapter that carries the default route, as the operating system reports it. Hardware
/// (MAC) addresses are deliberately left out: they identify the device and add nothing to diagnosis.
/// </summary>
public sealed record ConnectionSnapshot(
    string AdapterName,
    string AdapterDescription,
    string AdapterKind,
    long? LinkSpeedBitsPerSecond,
    IReadOnlyList<string> LocalAddresses,
    IReadOnlyList<string> Gateways,
    IReadOnlyList<string> DnsServers);
