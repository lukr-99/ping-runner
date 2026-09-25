namespace PingRunner.Core.Throughput;

/// <summary>The rate between two samples, placed at the later one; for drawing a throughput line.</summary>
public sealed record ThroughputPoint(TimeSpan Elapsed, double BitsPerSecond);
