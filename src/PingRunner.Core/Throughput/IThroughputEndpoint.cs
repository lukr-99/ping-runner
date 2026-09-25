namespace PingRunner.Core.Throughput;

/// <summary>
/// A server to move test data to and from. Each call moves at most the given number of bytes and
/// reports every chunk as it goes, so the meter sees the rate while a transfer is still running.
/// </summary>
public interface IThroughputEndpoint
{
    /// <summary>Shown next to the results, for example "Cloudflare (speed.cloudflare.com)".</summary>
    string Name { get; }

    Task DownloadAsync(long bytes, Action<int> onBytes, CancellationToken cancellationToken);

    Task UploadAsync(long bytes, Action<int> onBytes, CancellationToken cancellationToken);
}
