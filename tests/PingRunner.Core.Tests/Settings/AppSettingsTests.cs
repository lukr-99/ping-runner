using PingRunner.Core.Settings;

namespace PingRunner.Core.Tests.Settings;

public sealed class AppSettingsTests
{
    [Fact]
    public void WithRecentTarget_MovesTheHostToTheFrontWithoutDuplicates()
    {
        var settings = AppSettings.Default.WithRecentTarget("1.1.1.1").WithRecentTarget(" 8.8.8.8 ");

        Assert.Equal("8.8.8.8", settings.Target);
        Assert.Equal(["8.8.8.8", "1.1.1.1"], settings.RecentTargets);
    }

    [Fact]
    public void WithRecentTarget_KeepsAtMostEight()
    {
        var settings = Enumerable.Range(1, 12).Aggregate(AppSettings.Default, (current, index) => current.WithRecentTarget($"10.0.0.{index}"));

        Assert.Equal(AppSettings.MaximumRecentTargets, settings.RecentTargets.Count);
        Assert.Equal("10.0.0.12", settings.RecentTargets[0]);
    }

    [Fact]
    public void WithRecentTarget_BlankHost_ChangesNothing()
    {
        Assert.Same(AppSettings.Default, AppSettings.Default.WithRecentTarget("  "));
    }

    [Fact]
    public void Normalized_OutOfRangeValues_AreClamped()
    {
        var settings = new AppSettings
        {
            Version = 99,
            Theme = (ThemeMode)42,
            Target = "bad host",
            RecentTargets = ["a", "A", " ", "b"],
            IntervalMilliseconds = 1,
            TimeoutMilliseconds = 999_999,
            Duration = "soon",
            SpeedTestStreams = 100,
        }.Normalized();

        Assert.Equal(AppSettings.CurrentVersion, settings.Version);
        Assert.Equal(ThemeMode.System, settings.Theme);
        Assert.Equal(AppSettings.DefaultTarget, settings.Target);
        Assert.Equal(["a", "b"], settings.RecentTargets);
        Assert.Equal(100, settings.IntervalMilliseconds);
        Assert.Equal(60_000, settings.TimeoutMilliseconds);
        Assert.Equal(AppSettings.DefaultDuration, settings.Duration);
        Assert.Equal(16, settings.SpeedTestStreams);
    }
}
