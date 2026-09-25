using System.Net;
using PingRunner.Infrastructure.Tests.Fakes;
using PingRunner.Infrastructure.Throughput;

namespace PingRunner.Infrastructure.Tests.Throughput;

public sealed class CloudflareSpeedEndpointTests
{
    [Fact]
    public async Task DownloadAsync_AsksForTheBytesAndCountsWhatArrives()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[300_000]) });
        long counted = 0;

        await new CloudflareSpeedEndpoint(new HttpClient(handler)).DownloadAsync(300_000, count => counted += count, TestContext.Current.CancellationToken);

        Assert.Equal(300_000, counted);
        Assert.Equal("https://speed.cloudflare.com/__down?bytes=300000", Assert.Single(handler.Requests).RequestUri!.ToString());
    }

    [Fact]
    public async Task UploadAsync_PostsTheBytesAndCountsThem()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        long counted = 0;

        await new CloudflareSpeedEndpoint(new HttpClient(handler)).UploadAsync(200_000, count => counted += count, TestContext.Current.CancellationToken);

        Assert.Equal(200_000, counted);
        Assert.Equal(200_000, Assert.Single(handler.UploadedBodyLengths));
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://speed.cloudflare.com/__up", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task DownloadAsync_ServerError_Throws()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            new CloudflareSpeedEndpoint(new HttpClient(handler)).DownloadAsync(1_000, _ => { }, TestContext.Current.CancellationToken));
    }
}
