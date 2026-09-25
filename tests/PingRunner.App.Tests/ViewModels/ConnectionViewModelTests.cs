using PingRunner.App.Shell;
using PingRunner.App.Tests.Hosting;

namespace PingRunner.App.Tests.ViewModels;

[Collection(WpfCollection.Name)]
public sealed class ConnectionViewModelTests
{
    [Fact]
    public Task EnsureLoaded_ShowsTheAdapterAndKeepsThePublicIpHidden() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create();
        var connection = app.Graph.Connection;

        await connection.EnsureLoadedAsync();

        Assert.Equal("Wi-Fi", connection.AdapterName);
        Assert.Equal("1.2 Gbit/s", connection.LinkSpeed.Replace(',', '.'));
        Assert.Equal("192.168.1.1", connection.Gateway);
        Assert.Equal("Hidden", connection.PublicIpText);
        Assert.False(connection.CopyPublicIpCommand.CanExecute(null));
    });

    [Fact]
    public Task TogglePublicIp_RevealsAndCopies() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create();
        var connection = app.Graph.Connection;
        await connection.EnsureLoadedAsync();

        connection.TogglePublicIpCommand.Execute(null);
        connection.CopyPublicIpCommand.Execute(null);

        Assert.Equal("203.0.113.42", connection.PublicIpText);
        Assert.Equal("Hide", connection.PublicIpToggleText);
        Assert.Equal(["203.0.113.42"], app.Desktop.Copied);
    });

    [Fact]
    public Task OpenRouter_OpensTheGatewayAdminPageInTheBrowser() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create();
        var connection = app.Graph.Connection;
        Assert.False(connection.OpenRouterCommand.CanExecute(null));

        await connection.EnsureLoadedAsync();
        connection.OpenRouterCommand.Execute(null);

        Assert.Equal(["http://192.168.1.1/"], app.Desktop.Opened);
        Assert.Contains("http://192.168.1.1/", connection.RouterHint, StringComparison.Ordinal);
    });

    [Fact]
    public Task PingGateway_FillsTheMonitorAndOpensIt() => WpfHost.RunAsync(async () =>
    {
        using var app = TestApp.Create();
        var opened = new List<AppPage>();
        app.Graph.Navigation.Requested += (_, page) => opened.Add(page);
        await app.Graph.Connection.EnsureLoadedAsync();

        app.Graph.Connection.PingGatewayCommand.Execute(null);

        Assert.Equal("192.168.1.1", app.Graph.Monitor.Target);
        Assert.Equal([AppPage.Monitor], opened);
    });
}
