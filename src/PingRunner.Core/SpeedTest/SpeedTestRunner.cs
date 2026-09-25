using System.Collections.Concurrent;
using PingRunner.Core.Pinging;
using PingRunner.Core.Statistics;
using PingRunner.Core.Throughput;

namespace PingRunner.Core.SpeedTest;

/// <summary>
/// Runs a speed test end to end: a few pings on an idle line, then the download and the upload with
/// pings continuing underneath, so the result shows both the line's capacity and what filling it does
/// to latency.
/// </summary>
public sealed class SpeedTestRunner(IThroughputEndpoint endpoint, IPingSender pinger, TimeProvider time)
{
    private readonly ThroughputTest throughput = new(endpoint, time);

    public async Task<SpeedTestResult> RunAsync(
        SpeedTestOptions options,
        IProgress<SpeedTestProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var startedAt = time.GetLocalNow();

        var idle = new ConcurrentQueue<double>();
        for (var ping = 0; ping < options.IdlePings; ping++)
        {
            var latency = await PingOnceAsync(options, cancellationToken).ConfigureAwait(false);
            if (latency is { } value)
            {
                idle.Enqueue(value);
            }

            progress?.Report(new SpeedTestProgress(SpeedTestPhase.IdleLatency, null, latency));

            // Three silent pings in a row: the host does not answer ICMP here, so stop waiting on it.
            if (idle.IsEmpty && ping >= 2)
            {
                break;
            }

            await Task.Delay(options.PingInterval, time, cancellationToken).ConfigureAwait(false);
        }

        var (download, downloadLatency) = await RunLoadedAsync(
            SpeedTestPhase.Download, ThroughputDirection.Download, options, progress, cancellationToken).ConfigureAwait(false);
        var (upload, uploadLatency) = await RunLoadedAsync(
            SpeedTestPhase.Upload, ThroughputDirection.Upload, options, progress, cancellationToken).ConfigureAwait(false);

        return new SpeedTestResult(
            startedAt,
            endpoint.Name,
            options.LatencyHost,
            LatencyDistribution.From(idle),
            download,
            downloadLatency,
            upload,
            uploadLatency);
    }

    private async Task<(ThroughputResult Result, LatencyDistribution? Latency)> RunLoadedAsync(
        SpeedTestPhase phase,
        ThroughputDirection direction,
        SpeedTestOptions options,
        IProgress<SpeedTestProgress>? progress,
        CancellationToken cancellationToken)
    {
        var loaded = new ConcurrentQueue<double>();
        double? latest = null;
        using var pinging = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pings = PingUntilCancelledAsync(options, loaded, value => latest = value, pinging.Token);

        try
        {
            var result = await throughput.RunAsync(
                direction,
                options.Throughput,
                progress is null ? null : new InlineProgress<ThroughputProgress>(
                    running => progress.Report(new SpeedTestProgress(phase, running, latest))),
                cancellationToken).ConfigureAwait(false);
            return (result, LatencyDistribution.From(loaded));
        }
        finally
        {
            await pinging.CancelAsync().ConfigureAwait(false);
            await pings.ConfigureAwait(false);
        }
    }

    private async Task PingUntilCancelledAsync(
        SpeedTestOptions options,
        ConcurrentQueue<double> samples,
        Action<double?> onSample,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var latency = await PingOnceAsync(options, cancellationToken).ConfigureAwait(false);
                if (latency is { } value)
                {
                    samples.Enqueue(value);
                }

                onSample(latency);
                await Task.Delay(options.PingInterval, time, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The transfer finished; loaded pings stop with it.
        }
    }

    private async Task<double?> PingOnceAsync(SpeedTestOptions options, CancellationToken cancellationToken)
    {
        var outcome = await pinger.SendAsync(options.LatencyHost, options.PingTimeout, cancellationToken).ConfigureAwait(false);
        return outcome is { IsSuccess: true, RoundtripMilliseconds: { } roundtrip } ? roundtrip : null;
    }
}
