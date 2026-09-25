using PingRunner.Core.SpeedTest;

namespace PingRunner.Core.History;

/// <summary>One stored speed test, complete with its latency samples and throughput series.</summary>
public sealed record SpeedTestRecord(long Id, SpeedTestResult Result);
