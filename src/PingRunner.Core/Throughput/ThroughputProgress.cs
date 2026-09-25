namespace PingRunner.Core.Throughput;

/// <summary>A running transfer's state, reported after every sample.</summary>
public sealed record ThroughputProgress(
    ThroughputDirection Direction,
    TimeSpan Elapsed,
    TimeSpan Duration,
    double CurrentBitsPerSecond,
    long TotalBytes);
