using System.Net;
using System.Net.Http.Headers;

namespace PingRunner.Infrastructure.Throughput;

/// <summary>
/// An upload body of a fixed length that reports every chunk as it is written, so the meter sees the
/// upload rate while the request is still going. The bytes are pseudo-random so nothing on the path
/// can compress them.
/// </summary>
internal sealed class CountingUploadContent : HttpContent
{
    private const int ChunkSize = 64 * 1024;
    private static readonly byte[] Chunk = CreateChunk();
    private readonly long length;
    private readonly Action<int> onBytes;

    public CountingUploadContent(long length, Action<int> onBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentNullException.ThrowIfNull(onBytes);
        this.length = length;
        this.onBytes = onBytes;
        Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        var remaining = length;
        while (remaining > 0)
        {
            var count = (int)Math.Min(ChunkSize, remaining);
            await stream.WriteAsync(Chunk.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
            onBytes(count);
            remaining -= count;
        }
    }

    protected override bool TryComputeLength(out long length)
    {
        length = this.length;
        return true;
    }

    private static byte[] CreateChunk()
    {
        var chunk = new byte[ChunkSize];
#pragma warning disable CA5394 // Filler bytes, not security: they only need to resist compression.
        new Random(20260925).NextBytes(chunk);
#pragma warning restore CA5394
        return chunk;
    }
}
