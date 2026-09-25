namespace PingRunner.Core.Pinging;

/// <summary>
/// One echo request and what came back. A failed attempt (timeout, unreachable, resolve error) has no
/// round-trip time; <see cref="Details"/> says why it failed or which address replied.
/// </summary>
public sealed record PingAttempt(
    string TargetHost,
    DateTimeOffset Timestamp,
    bool IsSuccess,
    long? RoundtripMilliseconds,
    string Details);
