namespace PingRunner.Core.Throughput;

/// <summary>
/// One direction's outcome. <see cref="AverageBitsPerSecond"/> leaves out the warm-up;
/// <see cref="PeakBitsPerSecond"/> is the best rate held over the peak window (one second by default),
/// so a single lucky sample cannot inflate it.
/// </summary>
public sealed record ThroughputResult(
    ThroughputDirection Direction,
    double AverageBitsPerSecond,
    double PeakBitsPerSecond,
    long TotalBytes,
    TimeSpan Duration,
    IReadOnlyList<ThroughputPoint> Series);
