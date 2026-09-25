using PingRunner.Core.Network;

namespace PingRunner.Core.Tests.Network;

public sealed class RouterAdminPageTests
{
    [Fact]
    public void UrlFor_Ipv4Gateway_IsPlainHttp()
    {
        Assert.Equal(new Uri("http://192.168.1.1/"), RouterAdminPage.UrlFor("192.168.1.1"));
    }

    [Fact]
    public void UrlFor_Ipv6GatewayWithZone_IsBracketedWithoutTheZone()
    {
        Assert.Equal("http://[fe80::1]/", RouterAdminPage.UrlFor("fe80::1%12")!.AbsoluteUri);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("router.local")]
    [InlineData("javascript:alert(1)")]
    public void UrlFor_NotAnIpAddress_IsNull(string? gateway)
    {
        Assert.Null(RouterAdminPage.UrlFor(gateway));
    }
}
