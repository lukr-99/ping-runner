using PingRunner.App.Composition;

namespace PingRunner.App.Tests.ViewModels;

public sealed class BuildInfoTests
{
    [Fact]
    public void FromAssembly_LocalBuild_IsADevBuild()
    {
        var build = BuildInfo.FromAssembly(typeof(App).Assembly);

        Assert.True(build.IsDevBuild);
        Assert.Equal("dev", build.Version.PreRelease);
    }
}
