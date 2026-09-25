using PingRunner.Core.Throughput;

namespace PingRunner.Core.Tests.Fakes;

/// <summary>
/// A transfer that moves bytes only when the test says so: every stream registers its byte callback
/// and then waits until the test is cancelled.
/// </summary>
public sealed class ManualThroughputEndpoint : IThroughputEndpoint
{
    private readonly List<Action<int>> streams = [];

    public string Name => "Manual";

    public int StreamCount
    {
        get
        {
            lock (streams)
            {
                return streams.Count;
            }
        }
    }

    public Task DownloadAsync(long bytes, Action<int> onBytes, CancellationToken cancellationToken) => Hold(onBytes, cancellationToken);

    public Task UploadAsync(long bytes, Action<int> onBytes, CancellationToken cancellationToken) => Hold(onBytes, cancellationToken);

    /// <summary>Each registered stream reports <paramref name="bytesPerStream"/> bytes.</summary>
    public void Move(int bytesPerStream)
    {
        lock (streams)
        {
            foreach (var stream in streams)
            {
                stream(bytesPerStream);
            }
        }
    }

    public void WaitForStreams(int count)
    {
        if (!SpinWait.SpinUntil(() => StreamCount >= count, TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException($"Only {StreamCount} of {count} streams started.");
        }
    }

    private Task Hold(Action<int> onBytes, CancellationToken cancellationToken)
    {
        lock (streams)
        {
            streams.Add(onBytes);
        }

        return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }
}
