using PingRunner.App.Tests.Hosting;

namespace PingRunner.App.Tests.ViewModels;

[Collection(WpfCollection.Name)]
public sealed class SpeedTestViewModelTests
{
    [Fact]
    public Task Run_Finishes_KeepsTheResultOnTopOfTheHistory() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create(Core.Settings.AppSettings.Default with { SpeedTestSeconds = 3 });
        var speed = app.Graph.SpeedTest;

        await app.RunSpeedTestAsync();

        Assert.False(speed.IsRunning);
        Assert.NotNull(speed.Latest);
        Assert.Same(speed.Latest, Assert.Single(speed.History));
        Assert.Equal(1, speed.Progress);
        Assert.Equal("A+", speed.Latest.Grade);
        Assert.NotEmpty(speed.DownloadPoints);
        Assert.NotEmpty(speed.UploadPoints);
        Assert.EndsWith("Mbps", speed.Latest.DownloadShort, StringComparison.Ordinal);
    });
}
