using PingRunner.Core.Updates;

namespace PingRunner.Core.Tests.Updates;

public sealed class ReleaseVersionTests
{
    [Theory]
    [InlineData("2.0.0", 2, 0, 0, "")]
    [InlineData("v2.1.3", 2, 1, 3, "")]
    [InlineData("2.0.0-dev", 2, 0, 0, "dev")]
    [InlineData("2.0.0-rc.1+build.7", 2, 0, 0, "rc.1")]
    public void TryParse_ValidVersion_ReadsEveryPart(string text, int major, int minor, int patch, string preRelease)
    {
        Assert.Equal(new ReleaseVersion(major, minor, patch, preRelease), ReleaseVersion.TryParse(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2.0")]
    [InlineData("latest")]
    public void TryParse_NotAVersion_IsNull(string? text)
    {
        Assert.Null(ReleaseVersion.TryParse(text));
    }

    [Theory]
    [InlineData("2.0.0", "1.9.9")]
    [InlineData("2.0.0", "2.0.0-dev")]
    [InlineData("2.0.1-dev", "2.0.0")]
    [InlineData("10.0.0", "9.0.0")]
    public void CompareTo_OrdersVersions(string newer, string older)
    {
        Assert.True(ReleaseVersion.TryParse(newer)! > ReleaseVersion.TryParse(older)!);
        Assert.True(ReleaseVersion.TryParse(older)! < ReleaseVersion.TryParse(newer)!);
    }

    [Fact]
    public void ToString_RoundTrips()
    {
        Assert.Equal("2.0.0-dev", ReleaseVersion.TryParse("v2.0.0-dev")!.ToString());
    }
}
