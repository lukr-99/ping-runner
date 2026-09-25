using PingRunner.Core.Pinging;
using PingRunner.Core.Tests.Fakes;

namespace PingRunner.Core.Tests.Pinging;

public sealed class PingSpacingTests
{
    [Fact]
    public void Typical_IsTheMedianGap_InTimeOrder()
    {
        // Gaps of 1, 1, 1 and 30 seconds, given out of order.
        var attempts = new[] { Attempts.At(3, 10), Attempts.At(0, 10), Attempts.At(33, 10), Attempts.At(1, 10), Attempts.At(2, 10) };

        Assert.Equal(TimeSpan.FromSeconds(1), PingSpacing.Typical(attempts));
    }

    [Fact]
    public void Typical_OnePing_IsUnknown()
    {
        Assert.Null(PingSpacing.Typical([Attempts.At(0, 10)]));
    }
}
