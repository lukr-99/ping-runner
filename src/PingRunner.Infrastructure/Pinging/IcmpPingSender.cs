using System.Net.NetworkInformation;
using System.Net.Sockets;
using PingRunner.Core.Pinging;

namespace PingRunner.Infrastructure.Pinging;

/// <summary>
/// ICMP echo through the operating system, with the same 32-byte payload Windows' ping uses. A fresh
/// <see cref="Ping"/> per request lets the monitor and the speed test's latency probe send at the
/// same time.
/// </summary>
public sealed class IcmpPingSender : IPingSender
{
    private static readonly byte[] Payload = new byte[32];

    public async Task<PingOutcome> SendAsync(string host, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var ping = new Ping();
        try
        {
            var reply = await ping.SendPingAsync(host, timeout, Payload, new PingOptions(128, false), cancellationToken)
                .ConfigureAwait(false);
            return reply.Status == IPStatus.Success
                ? PingOutcome.Reply(reply.RoundtripTime, reply.Address.ToString())
                : PingOutcome.Failure(Describe(reply.Status));
        }
        catch (PingException exception)
        {
            return PingOutcome.Failure(exception.InnerException?.Message ?? exception.Message);
        }
        catch (Exception exception) when (exception is SocketException or ArgumentException or InvalidOperationException)
        {
            return PingOutcome.Failure(exception.Message);
        }
    }

    private static string Describe(IPStatus status) => status switch
    {
        IPStatus.TimedOut => "Request timed out",
        IPStatus.DestinationHostUnreachable => "Destination host unreachable",
        IPStatus.DestinationNetworkUnreachable => "Destination network unreachable",
        IPStatus.DestinationPortUnreachable => "Destination port unreachable",
        IPStatus.DestinationProhibited => "Destination prohibited",
        IPStatus.TtlExpired => "TTL expired in transit",
        IPStatus.PacketTooBig => "Packet too big",
        IPStatus.BadRoute => "Bad route",
        IPStatus.HardwareError => "Hardware error",
        _ => $"Ping status: {status}",
    };
}
