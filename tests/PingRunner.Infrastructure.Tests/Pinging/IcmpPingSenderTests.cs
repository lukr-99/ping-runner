using PingRunner.Infrastructure.Pinging;

namespace PingRunner.Infrastructure.Tests.Pinging;

/// <summary>Loopback only: the machine answers itself, so this needs no network.</summary>
public sealed class IcmpPingSenderTests
{
    [Fact]
    public async Task SendAsync_Loopback_Replies()
    {
        var outcome = await new IcmpPingSender().SendAsync("127.0.0.1", TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.True(outcome.IsSuccess, outcome.Details);
        Assert.NotNull(outcome.RoundtripMilliseconds);
        Assert.Equal("Reply from 127.0.0.1", outcome.Details);
    }
}
