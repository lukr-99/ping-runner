namespace PingRunner.Core.Throughput;

/// <summary>All bytes moved so far, read at <see cref="Elapsed"/> since the transfer began.</summary>
public sealed record ThroughputSample(TimeSpan Elapsed, long TotalBytes);
