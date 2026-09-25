using PingRunner.Core.SpeedTest;

namespace PingRunner.Core.Tests.SpeedTest;

public sealed class BufferbloatTests
{
    [Theory]
    [InlineData(0, "A+")]
    [InlineData(4.9, "A+")]
    [InlineData(5, "A")]
    [InlineData(29, "A")]
    [InlineData(45, "B")]
    [InlineData(150, "C")]
    [InlineData(399, "D")]
    [InlineData(400, "F")]
    public void Grade_FollowsTheLatencyIncrease(double increase, string grade)
    {
        Assert.Equal(grade, new Bufferbloat(increase).GradeText);
    }
}
