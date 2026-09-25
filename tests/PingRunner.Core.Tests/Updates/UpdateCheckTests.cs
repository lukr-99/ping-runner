using PingRunner.Core.Tests.Fakes;
using PingRunner.Core.Updates;

namespace PingRunner.Core.Tests.Updates;

public sealed class UpdateCheckTests
{
    private static readonly ReleaseVersion Running = ReleaseVersion.TryParse("2.0.0")!;

    [Fact]
    public async Task CheckAsync_NewerRelease_OffersIt()
    {
        var result = await Check(new StaticReleaseSource(Release("2.1.0")));

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("2.1.0", result.Release!.Version.ToString());
    }

    [Theory]
    [InlineData("2.0.0")]
    [InlineData("1.1.0")]
    public async Task CheckAsync_SameOrOlderRelease_IsUpToDate(string version)
    {
        var result = await Check(new StaticReleaseSource(Release(version)));

        Assert.Equal(UpdateStatus.UpToDate, result.Status);
    }

    [Fact]
    public async Task CheckAsync_DevBuildOfTheReleasedVersion_OffersTheRelease()
    {
        var check = new UpdateCheck(new StaticReleaseSource(Release("2.0.0")), ReleaseVersion.TryParse("2.0.0-dev")!);

        Assert.Equal(UpdateStatus.UpdateAvailable, (await check.CheckAsync(TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task CheckAsync_NothingPublished_SaysSo()
    {
        Assert.Equal(UpdateStatus.NoReleases, (await Check(new StaticReleaseSource(null))).Status);
    }

    [Fact]
    public async Task CheckAsync_Offline_ReportsTheFailure()
    {
        var result = await Check(new StaticReleaseSource(null, new HttpRequestException("No route to host")));

        Assert.Equal(UpdateStatus.Failed, result.Status);
        Assert.Equal("No route to host", result.Error);
    }

    private static Task<UpdateCheckResult> Check(IReleaseSource source) =>
        new UpdateCheck(source, Running).CheckAsync(CancellationToken.None);

    private static ReleaseInfo Release(string version) =>
        new(ReleaseVersion.TryParse(version)!, $"Ping Runner {version}", new Uri($"https://github.com/lukr-99/ping-runner/releases/tag/v{version}"), null);
}
