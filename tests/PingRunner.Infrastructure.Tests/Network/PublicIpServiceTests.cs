using System.Net;
using PingRunner.Infrastructure.Network;
using PingRunner.Infrastructure.Tests.Fakes;

namespace PingRunner.Infrastructure.Tests.Network;

public sealed class PublicIpServiceTests
{
    [Fact]
    public async Task GetAsync_FirstServiceAnswers_ReturnsTheAddress()
    {
        var handler = StubHttpHandler.Text("203.0.113.7\n");

        Assert.Equal("203.0.113.7", await new PublicIpService(new HttpClient(handler)).GetAsync(TestContext.Current.CancellationToken));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetAsync_FirstServiceFails_AsksTheSecond()
    {
        var handler = new StubHttpHandler(request => request.RequestUri!.Host == "api.ipify.org"
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("2001:db8::1") });

        Assert.Equal("2001:db8::1", await new PublicIpService(new HttpClient(handler)).GetAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task GetAsync_AnswerIsNotAnAddress_IsNull()
    {
        var handler = StubHttpHandler.Text("<html>captive portal</html>");

        Assert.Null(await new PublicIpService(new HttpClient(handler)).GetAsync(TestContext.Current.CancellationToken));
    }
}
