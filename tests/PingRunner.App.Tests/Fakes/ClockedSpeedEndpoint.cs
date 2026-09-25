using PingRunner.Core.Throughput;

namespace PingRunner.App.Tests.Fakes;

/// <summary>
/// Moves bytes on the given clock at a rate that ramps up over the first second, like TCP does:
/// download near 480 Mbps and upload near 42 Mbps across four streams.
/// </summary>
public sealed class ClockedSpeedEndpoint(TimeProvider time) : IThroughputEndpoint
{
    public string Name => "Cloudflare (speed.cloudflare.com)";

    public Task DownloadAsync(long bytes, Action<int> onBytes, CancellationToken cancellationToken) =>
        Pump(bytesPerTickPerStream: 150_000, onBytes, cancellationToken);

    public Task UploadAsync(long bytes, Action<int> onBytes, CancellationToken cancellationToken) =>
        Pump(bytesPerTickPerStream: 13_000, onBytes, cancellationToken);

    private async Task Pump(int bytesPerTickPerStream, Action<int> onBytes, CancellationToken cancellationToken)
    {
        var started = time.GetTimestamp();
        var tick = 0;
        while (true)
        {
            var ramp = Math.Min(1, time.GetElapsedTime(started).TotalSeconds + 0.3);
            var wobble = 1 + (0.06 * Math.Sin(tick++ / 3d));
            onBytes((int)(bytesPerTickPerStream * ramp * wobble));
            await Task.Delay(TimeSpan.FromMilliseconds(10), time, cancellationToken).ConfigureAwait(false);
        }
    }
}
