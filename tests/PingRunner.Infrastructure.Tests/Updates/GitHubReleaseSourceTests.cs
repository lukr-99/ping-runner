using System.Net;
using PingRunner.Infrastructure.Tests.Fakes;
using PingRunner.Infrastructure.Updates;

namespace PingRunner.Infrastructure.Tests.Updates;

public sealed class GitHubReleaseSourceTests
{
    [Fact]
    public async Task GetLatestAsync_PublishedRelease_ReadsTheSample()
    {
        var handler = StubHttpHandler.Text(await File.ReadAllTextAsync(Path.Combine("Samples", "github-release.json"), TestContext.Current.CancellationToken));

        var release = await Source(handler).GetLatestAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(release);
        Assert.Equal("2.1.0", release.Version.ToString());
        Assert.Equal("Ping Runner 2.1.0", release.Title);
        Assert.Equal(new Uri("https://github.com/lukr-99/ping-runner/releases/tag/v2.1.0"), release.PageUrl);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.github.com/repos/lukr-99/ping-runner/releases/latest", request.RequestUri!.ToString());
        Assert.Contains("PingRunner", request.Headers.UserAgent.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLatestAsync_NoReleases_IsNull()
    {
        var handler = StubHttpHandler.Text("""{ "message": "Not Found" }""", HttpStatusCode.NotFound);

        Assert.Null(await Source(handler).GetLatestAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("""{ "tag_name": "nightly", "html_url": "https://github.com/lukr-99/ping-runner/releases/tag/nightly" }""")]
    [InlineData("""{ "tag_name": "v2.1.0", "html_url": "https://example.com/phish" }""")]
    [InlineData("""{ "tag_name": "v2.1.0", "html_url": "http://github.com/lukr-99/ping-runner" }""")]
    public async Task GetLatestAsync_UntrustworthyRelease_IsRejected(string json)
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => Source(StubHttpHandler.Text(json)).GetLatestAsync(TestContext.Current.CancellationToken));
    }

    private static GitHubReleaseSource Source(StubHttpHandler handler) =>
        new(new HttpClient(handler), "lukr-99", "ping-runner", "PingRunner/2.0.0");
}
