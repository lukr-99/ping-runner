using PingRunner.Core.Throughput;

namespace PingRunner.Core.SpeedTest;

/// <summary>
/// What the speed test is doing: which phase, the running transfer (none while measuring idle
/// latency) and the latest latency sample, if one came back.
/// </summary>
public sealed record SpeedTestProgress(
    SpeedTestPhase Phase,
    ThroughputProgress? Throughput,
    double? LatestLatencyMilliseconds);
