using PingRunner.Core.Statistics;
using PingRunner.Core.Throughput;

namespace PingRunner.Core.SpeedTest;

/// <summary>
/// One finished speed test. Loaded latencies are the pings sent while each transfer ran; the
/// bufferbloat is the worse of the two rises in median latency over idle.
/// </summary>
public sealed record SpeedTestResult(
    DateTimeOffset StartedAt,
    string Server,
    string LatencyHost,
    LatencyDistribution? IdleLatency,
    ThroughputResult Download,
    LatencyDistribution? DownloadLatency,
    ThroughputResult Upload,
    LatencyDistribution? UploadLatency)
{
    public long TotalBytes => Download.TotalBytes + Upload.TotalBytes;

    /// <summary>Null when the latency host did not answer idle or under load (ICMP blocked).</summary>
    public Bufferbloat? Bufferbloat
    {
        get
        {
            if (IdleLatency is null)
            {
                return null;
            }

            var loaded = new[] { DownloadLatency?.Median, UploadLatency?.Median }.Where(median => median is not null).Max();
            return loaded is { } median ? new Bufferbloat(Math.Max(0, median - IdleLatency.Median)) : null;
        }
    }
}
