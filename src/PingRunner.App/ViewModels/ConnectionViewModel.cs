using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PingRunner.App.Desktop;
using PingRunner.App.Formatting;
using PingRunner.App.Shell;
using PingRunner.Core.Network;

namespace PingRunner.App.ViewModels;

/// <summary>
/// The Connection page: the adapter carrying the default route, its gateway and DNS servers, and the
/// public address. The public address is looked up only when this page asks, and stays hidden until
/// shown, so it never lands in a screenshot by accident.
/// </summary>
public sealed partial class ConnectionViewModel(
    IConnectionInfoSource connections,
    IPublicIpSource publicIp,
    MonitorViewModel monitor,
    ShellNavigation navigation,
    IDesktopServices desktop) : ObservableObject
{
    private string? publicAddress;
    private bool loaded;

    [ObservableProperty]
    private bool isOffline;

    [ObservableProperty]
    private string adapterName = Units.None;

    [ObservableProperty]
    private string adapterDescription = string.Empty;

    [ObservableProperty]
    private string adapterKind = Units.None;

    [ObservableProperty]
    private string linkSpeed = Units.None;

    [ObservableProperty]
    private string localAddresses = Units.None;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PingGatewayCommand))]
    private string? gateway;

    [ObservableProperty]
    private string dnsServers = Units.None;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PingDnsCommand))]
    private string? firstDnsServer;

    [ObservableProperty]
    private string publicIpText = "Not looked up yet";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyPublicIpCommand))]
    private bool isPublicIpVisible;

    [ObservableProperty]
    private bool isRefreshing;

    public IReadOnlyList<QuickTarget> QuickTargets => QuickTarget.Defaults;

    public string PublicIpToggleText => IsPublicIpVisible ? "Hide" : "Show";

    /// <summary>Loads everything the first time the page opens.</summary>
    public async Task EnsureLoadedAsync()
    {
        if (!loaded)
        {
            loaded = true;
            await RefreshAsync().ConfigureAwait(true);
        }
    }

    partial void OnIsPublicIpVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(PublicIpToggleText));
        ShowPublicIp();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        try
        {
            var snapshot = connections.GetPrimary();
            IsOffline = snapshot is null;
            AdapterName = snapshot?.AdapterName ?? "No connection";
            AdapterDescription = snapshot?.AdapterDescription ?? "No adapter has a default route.";
            AdapterKind = snapshot?.AdapterKind ?? Units.None;
            LinkSpeed = Units.LinkSpeed(snapshot?.LinkSpeedBitsPerSecond);
            LocalAddresses = Join(snapshot?.LocalAddresses);
            Gateway = snapshot?.Gateways.FirstOrDefault();
            DnsServers = Join(snapshot?.DnsServers);
            FirstDnsServer = snapshot?.DnsServers.FirstOrDefault();

            PublicIpText = "Looking up…";
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            publicAddress = await publicIp.GetAsync(timeout.Token).ConfigureAwait(true);
            ShowPublicIp();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private void TogglePublicIp() => IsPublicIpVisible = !IsPublicIpVisible;

    [RelayCommand(CanExecute = nameof(IsPublicIpVisible))]
    private void CopyPublicIp()
    {
        if (publicAddress is not null)
        {
            desktop.CopyText(publicAddress);
        }
    }

    [RelayCommand(CanExecute = nameof(HasGateway))]
    private void PingGateway() => PingHost(Gateway);

    [RelayCommand(CanExecute = nameof(HasDnsServer))]
    private void PingDns() => PingHost(FirstDnsServer);

    [RelayCommand]
    private void PingHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return;
        }

        monitor.UseTarget(host);
        navigation.Open(AppPage.Monitor);
    }

    private bool HasGateway() => Gateway is not null;

    private bool HasDnsServer() => FirstDnsServer is not null;

    private void ShowPublicIp() => PublicIpText = publicAddress switch
    {
        null when !loaded => "Not looked up yet",
        null => "Unavailable",
        _ when IsPublicIpVisible => publicAddress,
        _ => "Hidden",
    };

    private static string Join(IReadOnlyList<string>? values) =>
        values is { Count: > 0 } ? string.Join(Environment.NewLine, values) : Units.None;
}
