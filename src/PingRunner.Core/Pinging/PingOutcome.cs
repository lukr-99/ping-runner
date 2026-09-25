namespace PingRunner.Core.Pinging;

/// <summary>What a single echo request returned, before the loop stamps it with a host and time.</summary>
public sealed record PingOutcome(bool IsSuccess, long? RoundtripMilliseconds, string Details)
{
    public static PingOutcome Reply(long roundtripMilliseconds, string address) =>
        new(true, Math.Max(0, roundtripMilliseconds), $"Reply from {address}");

    public static PingOutcome Failure(string details) => new(false, null, details);
}
