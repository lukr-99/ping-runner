using PingRunner.App.Tests.Hosting;

namespace PingRunner.App.Tests.ViewModels;

public sealed class AboutViewModelTests
{
    [Fact]
    public Task Version_ReleaseBuild_SaysRelease() => WpfHost.RunAsync(() =>
    {
        using var app = TestApp.Create();

        Assert.Equal("2.0.0", app.Graph.About.Version);
        Assert.Equal("Release 2.0.0", app.Graph.About.BuildLabel);
        return Task.CompletedTask;
    });

    [Fact]
    public Task Version_DevelopmentBuild_SaysDevelopmentBuild() => WpfHost.RunAsync(() =>
    {
        using var app = TestApp.Create(releaseBuild: false);

        Assert.Equal("Development build 2.0.0-dev", app.Graph.About.BuildLabel);
        return Task.CompletedTask;
    });

    [Fact]
    public Task OpenLink_OpensTheAddressInTheBrowser() => WpfHost.RunAsync(() =>
    {
        using var app = TestApp.Create();
        var link = app.Graph.About.Content.FeedbackLinks[0];

        app.Graph.About.OpenLinkCommand.Execute(link.Url);

        Assert.Equal([link.Url], app.Desktop.Opened);
        return Task.CompletedTask;
    });

    [Fact]
    public Task OpenLink_WithNothingToOpen_DoesNothing() => WpfHost.RunAsync(() =>
    {
        using var app = TestApp.Create();

        app.Graph.About.OpenLinkCommand.Execute(null);

        Assert.Empty(app.Desktop.Opened);
        return Task.CompletedTask;
    });

    [Fact]
    public Task OpenDataFolder_OpensTheSettingsFolder() => WpfHost.RunAsync(() =>
    {
        using var app = TestApp.Create();

        app.Graph.About.OpenDataFolderCommand.Execute(null);

        Assert.Equal([app.Graph.About.DataFolder], app.Desktop.Opened);
        return Task.CompletedTask;
    });
}
