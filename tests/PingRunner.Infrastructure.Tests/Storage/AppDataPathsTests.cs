using PingRunner.Infrastructure.Storage;

namespace PingRunner.Infrastructure.Tests.Storage;

public sealed class AppDataPathsTests
{
    [Fact]
    public void ForUser_DevBuild_UsesItsOwnFolder()
    {
        var release = AppDataPaths.ForUser(isDevBuild: false);
        var dev = AppDataPaths.ForUser(isDevBuild: true);

        Assert.EndsWith("PingRunner", release.Root, StringComparison.Ordinal);
        Assert.EndsWith("PingRunner Dev", dev.Root, StringComparison.Ordinal);
        Assert.Equal(Path.Combine(release.Root, "settings.json"), release.Settings);
    }
}
