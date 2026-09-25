using System.Buffers;
using PingRunner.Core.Throughput;

namespace PingRunner.Infrastructure.Throughput;

/// <summary>
/// Cloudflare's public speed-test service, the one behind speed.cloudflare.com: <c>__down?bytes=N</c>
/// streams N bytes and <c>__up</c> accepts a body of any size. The anycast network answers from a
/// nearby data centre, so the result reflects the local line rather than a long-haul path.
/// </summary>
public sealed class CloudflareSpeedEndpoint(HttpClient http) : IThroughputEndpoint
{
    private const int BufferSize = 64 * 1024;
    private static readonly Uri BaseUri = new("https://speed.cloudflare.com/");

    public string Name => "Cloudflare (speed.cloudflare.com)";

    public async Task DownloadAsync(long bytes, Action<int> onBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onBytes);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseUri, $"__down?bytes={bytes}"));
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
            try
            {
                int read;
                while ((read = await stream.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    onBytes(read);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    public async Task UploadAsync(long bytes, Action<int> onBytes, CancellationToken cancellationToken)
    {
        using var content = new CountingUploadContent(bytes, onBytes);
        using var response = await http.PostAsync(new Uri(BaseUri, "__up"), content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }
}
