using PingRunner.Core.Pinging;

namespace PingRunner.Core.Tests.Pinging;

public sealed class PingRunSettingsTests
{
    [Fact]
    public void TryCreate_ValidValues_TrimsHostAndConvertsUnits()
    {
        var settings = PingRunSettings.TryCreate("  1.1.1.1 ", 800, 500, TimeSpan.FromMinutes(2), out var error);

        Assert.Null(error);
        Assert.NotNull(settings);
        Assert.Equal("1.1.1.1", settings.TargetHost);
        Assert.Equal(TimeSpan.FromMilliseconds(800), settings.Timeout);
        Assert.Equal(TimeSpan.FromMilliseconds(500), settings.Interval);
        Assert.False(settings.RunsUntilStopped);
    }

    [Fact]
    public void TryCreate_NoDuration_RunsUntilStopped()
    {
        var settings = PingRunSettings.TryCreate("example.com", 1000, 1000, null, out _);

        Assert.True(settings!.RunsUntilStopped);
    }

    [Theory]
    [InlineData("", 1000, 1000, "Enter a host name")]
    [InlineData("my host", 1000, 1000, "cannot contain spaces")]
    [InlineData("8.8.8.8", 5, 1000, "Timeout must be between")]
    [InlineData("8.8.8.8", 1000, 50, "Interval must be between")]
    public void TryCreate_InvalidValue_ExplainsTheProblem(string host, int timeout, int interval, string expected)
    {
        var settings = PingRunSettings.TryCreate(host, timeout, interval, null, out var error);

        Assert.Null(settings);
        Assert.Contains(expected, error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryCreate_ZeroDuration_IsRejected()
    {
        var settings = PingRunSettings.TryCreate("8.8.8.8", 1000, 1000, TimeSpan.Zero, out var error);

        Assert.Null(settings);
        Assert.Contains("Duration", error, StringComparison.Ordinal);
    }
}
