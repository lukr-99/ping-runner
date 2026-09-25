namespace PingRunner.Core.Throughput;

/// <summary>
/// Measures one direction: runs parallel streams against the endpoint for the test's duration,
/// samples the byte count on a fixed interval and reports the rate as it goes. A stream that fails is
/// dropped; the test fails only when every stream has failed.
/// </summary>
public sealed class ThroughputTest(IThroughputEndpoint endpoint, TimeProvider time)
{
    private static readonly TimeSpan CurrentRateWindow = TimeSpan.FromSeconds(1);

    public async Task<ThroughputResult> RunAsync(
        ThroughputDirection direction,
        ThroughputTestOptions options,
        IProgress<ThroughputProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        long total = 0;
        var meter = new ThroughputMeter();
        using var transfer = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var startedAt = time.GetTimestamp();
        void OnBytes(int count) => Interlocked.Add(ref total, count);

        var streams = Enumerable.Range(0, options.Streams)
            .Select(_ => RunStreamAsync(direction, options.RequestBytes, OnBytes, transfer.Token))
            .ToArray();

        try
        {
            while (time.GetElapsedTime(startedAt) < options.Duration)
            {
                var remaining = options.Duration - time.GetElapsedTime(startedAt);
                await Task.Delay(remaining < options.SampleInterval ? remaining : options.SampleInterval, time, cancellationToken)
                    .ConfigureAwait(false);

                var elapsed = time.GetElapsedTime(startedAt);
                meter.Record(elapsed < options.Duration ? elapsed : options.Duration, Interlocked.Read(ref total));
                progress?.Report(new ThroughputProgress(
                    direction,
                    meter.Elapsed,
                    options.Duration,
                    meter.CurrentBitsPerSecond(CurrentRateWindow),
                    meter.TotalBytes));

                if (streams.All(stream => stream.IsFaulted))
                {
                    throw new ThroughputTestException(
                        $"The {direction.ToString().ToLowerInvariant()} test could not reach {endpoint.Name}.",
                        streams[0].Exception?.GetBaseException());
                }
            }
        }
        finally
        {
            await transfer.CancelAsync().ConfigureAwait(false);
            await Task.WhenAll(streams.Select(IgnoreFailure)).ConfigureAwait(false);
        }

        return meter.ToResult(direction, options);
    }

    private async Task RunStreamAsync(ThroughputDirection direction, long bytes, Action<int> onBytes, CancellationToken token)
    {
        await Task.Yield();
        while (!token.IsCancellationRequested)
        {
            try
            {
                var transfer = direction == ThroughputDirection.Download
                    ? endpoint.DownloadAsync(bytes, onBytes, token)
                    : endpoint.UploadAsync(bytes, onBytes, token);
                await transfer.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private static async Task IgnoreFailure(Task stream)
    {
        try
        {
            await stream.ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Already accounted for: a failed stream only ends the test when every stream failed.
        }
    }
}
