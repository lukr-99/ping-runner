using PingRunner.Core.Throughput;

namespace PingRunner.Core.Tests.Throughput;

public sealed class ThroughputMeterTests
{
    private const long Megabit = 125_000;

    [Fact]
    public void Record_OutOfOrderOrShrinking_IsIgnored()
    {
        var meter = new ThroughputMeter();
        meter.Record(TimeSpan.FromSeconds(1), 100);
        meter.Record(TimeSpan.FromSeconds(1), 200);
        meter.Record(TimeSpan.FromSeconds(2), 50);

        Assert.Equal(2, meter.Samples.Count);
        Assert.Equal(100, meter.TotalBytes);
    }

    [Fact]
    public void AverageBitsPerSecond_LeavesOutTheWarmUp()
    {
        // 1 Mbit/s during the first second, then 10 Mbit/s for two seconds.
        var meter = Meter((1, 1 * Megabit), (2, 11 * Megabit), (3, 21 * Megabit));

        Assert.Equal(10_000_000, meter.AverageBitsPerSecond(TimeSpan.FromSeconds(1)), 3);
        Assert.Equal(7_000_000, meter.AverageBitsPerSecond(TimeSpan.Zero), 3);
    }

    [Fact]
    public void AverageBitsPerSecond_EndedDuringWarmUp_UsesTheWholeTransfer()
    {
        var meter = Meter((0.5, 1 * Megabit));

        Assert.Equal(2_000_000, meter.AverageBitsPerSecond(TimeSpan.FromSeconds(1)), 3);
    }

    [Fact]
    public void PeakBitsPerSecond_IsTheBestFullWindow()
    {
        // Half-second samples: 4, 4, 20, 20, 4 Mbit/s. The best one-second stretch is 20 Mbit/s.
        var meter = Meter((0.5, 2 * Megabit), (1.0, 4 * Megabit), (1.5, 14 * Megabit), (2.0, 24 * Megabit), (2.5, 26 * Megabit));

        Assert.Equal(20_000_000, meter.PeakBitsPerSecond(TimeSpan.FromSeconds(1)), 3);
    }

    [Fact]
    public void PeakBitsPerSecond_ShorterThanTheWindow_UsesTheWholeTransfer()
    {
        var meter = Meter((0.2, 1 * Megabit), (0.4, 3 * Megabit));

        Assert.Equal(7_500_000, meter.PeakBitsPerSecond(TimeSpan.FromSeconds(1)), 3);
    }

    [Fact]
    public void CurrentBitsPerSecond_UsesTheMostRecentWindow()
    {
        var meter = Meter((1, 10 * Megabit), (2, 12 * Megabit), (3, 42 * Megabit));

        Assert.Equal(30_000_000, meter.CurrentBitsPerSecond(TimeSpan.FromSeconds(1)), 3);
    }

    [Fact]
    public void Series_HasARatePerSample()
    {
        var meter = Meter((1, 1 * Megabit), (2, 3 * Megabit));

        Assert.Equal([1_000_000d, 2_000_000d], meter.Series().Select(point => point.BitsPerSecond));
    }

    private static ThroughputMeter Meter(params (double Seconds, long Bytes)[] samples)
    {
        var meter = new ThroughputMeter();
        foreach (var (seconds, bytes) in samples)
        {
            meter.Record(TimeSpan.FromSeconds(seconds), bytes);
        }

        return meter;
    }
}
