using PingRunner.Core.Throughput;

namespace PingRunner.Core.Tests.Fakes;

/// <summary>Moves a fixed number of bytes every 10 ms of the given clock until cancelled.</summary>
public sealed class ClockedThroughputEndpoint(TimeProvider time, int bytesPerTick) : IThroughputEndpoint
{
    public string Name => "Clocked";

    public Task DownloadAsync(long bytes, Action<int> onBytes, CancellationToken cancellationToken) => Pump(onBytes, cancellationToken);

    public Task UploadAsync(long bytes, Action<int> onBytes, CancellationToken cancellationToken) => Pump(onBytes, cancellationToken);

    private async Task Pump(Action<int> onBytes, CancellationToken cancellationToken)
    {
        while (true)
        {
            onBytes(bytesPerTick);
            await Task.Delay(TimeSpan.FromMilliseconds(10), time, cancellationToken).ConfigureAwait(false);
        }
    }
}
