using PingRunner.Core.Throughput;

namespace PingRunner.Core.SpeedTest;

/// <summary>
/// A full speed test: idle latency first, then download and upload, each while pinging the latency
/// host to see how much a full line slows it down.
/// </summary>
public sealed record SpeedTestOptions
{
    /// <summary>Cloudflare's resolver answers from the same network as its speed-test servers.</summary>
    public const string DefaultLatencyHost = "1.1.1.1";

    public string LatencyHost { get; init; } = DefaultLatencyHost;

    public int IdlePings { get; init; } = 10;

    public TimeSpan PingInterval { get; init; } = TimeSpan.FromMilliseconds(250);

    public TimeSpan PingTimeout { get; init; } = TimeSpan.FromSeconds(1);

    public ThroughputTestOptions Throughput { get; init; } = ThroughputTestOptions.Default;
}
