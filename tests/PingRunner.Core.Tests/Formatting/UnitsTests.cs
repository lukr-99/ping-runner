using System.Globalization;
using PingRunner.Core.Formatting;

namespace PingRunner.Core.Tests.Formatting;

public sealed class UnitsTests : IDisposable
{
    private readonly CultureInfo previous = CultureInfo.CurrentCulture;

    public UnitsTests() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

    public void Dispose() => CultureInfo.CurrentCulture = previous;

    [Theory]
    [InlineData(null, "–")]
    [InlineData(4.25, "4.3 ms")]
    [InlineData(99.94, "99.9 ms")]
    [InlineData(312.4, "312 ms")]
    public void Milliseconds_OneDecimalBelowAHundred(double? value, string expected) =>
        Assert.Equal(expected, Units.Milliseconds(value));

    [Theory]
    [InlineData(0d, "0 %")]
    [InlineData(0.0004, "0.04 %")]
    [InlineData(0.0125, "1.3 %")]
    public void Percent_KeepsSmallLossVisible(double fraction, string expected) =>
        Assert.Equal(expected, Units.Percent(fraction));

    [Theory]
    [InlineData(850_000d, "850 kbps")]
    [InlineData(42_400_000d, "42.4 Mbps")]
    [InlineData(312_400_000d, "312 Mbps")]
    [InlineData(1_230_000_000d, "1.23 Gbps")]
    public void BitsPerSecond_PicksTheUnit(double rate, string expected) =>
        Assert.Equal(expected, Units.BitsPerSecond(rate));

    [Theory]
    [InlineData(4.2, "4.2 s")]
    [InlineData(192, "3 min 12 s")]
    [InlineData(7500, "2 h 05 min")]
    public void Span_ReadsNaturally(double seconds, string expected) =>
        Assert.Equal(expected, Units.Span(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void Clock_CountsPastADay() =>
        Assert.Equal("26:03:09", Units.Clock(new TimeSpan(1, 2, 3, 9)));

    [Theory]
    [InlineData(812_000_000L, "812 MB")]
    [InlineData(1_500_000_000L, "1.50 GB")]
    public void Bytes_UsesDecimalUnits(long bytes, string expected) =>
        Assert.Equal(expected, Units.Bytes(bytes));
}
