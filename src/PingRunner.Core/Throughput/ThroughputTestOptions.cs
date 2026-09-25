namespace PingRunner.Core.Throughput;

/// <summary>
/// How one direction of a throughput test runs: several parallel streams for a fixed time, sampled
/// often enough to find the best one-second stretch. The first part of the transfer is left out of the
/// average while TCP ramps up.
/// </summary>
public sealed record ThroughputTestOptions
{
    public static ThroughputTestOptions Default { get; } = new();

    public TimeSpan Duration { get; init; } = TimeSpan.FromSeconds(10);

    public int Streams { get; init; } = 4;

    public TimeSpan SampleInterval { get; init; } = TimeSpan.FromMilliseconds(200);

    public TimeSpan WarmUp { get; init; } = TimeSpan.FromSeconds(1);

    public TimeSpan PeakWindow { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Bytes per request; a stream sends another request when one finishes early.</summary>
    public long RequestBytes { get; init; } = 25_000_000;

    public static ThroughputTestOptions For(int seconds, int streams) => new()
    {
        Duration = TimeSpan.FromSeconds(Math.Clamp(seconds, 3, 60)),
        Streams = Math.Clamp(streams, 1, 16),
    };
}
