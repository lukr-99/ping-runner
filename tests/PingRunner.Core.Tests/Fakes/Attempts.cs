using PingRunner.Core.Pinging;

namespace PingRunner.Core.Tests.Fakes;

/// <summary>
/// Builds attempt series one second apart from a compact script: a number is a reply with that
/// round-trip time, null is a lost ping.
/// </summary>
public static class Attempts
{
    public static readonly DateTimeOffset Start = new(2026, 9, 25, 12, 0, 0, TimeSpan.FromHours(2));

    public static List<PingAttempt> Series(params long?[] roundtrips) =>
    [
        .. roundtrips.Select((roundtrip, index) => At(index, roundtrip)),
    ];

    public static PingAttempt At(int second, long? roundtrip, string host = "8.8.8.8") => new(
        host,
        Start.AddSeconds(second),
        roundtrip is not null,
        roundtrip,
        roundtrip is null ? "Request timed out" : $"Reply from {host}");
}
