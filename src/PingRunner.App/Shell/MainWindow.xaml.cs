using System.Windows.Threading;
using PingRunner.App.Composition;
using PingRunner.App.Views;

namespace PingRunner.App.Shell;

/// <summary>
/// The one window: a left navigation pane over the Monitor, Graph, Speed test, Connection, History and
/// Settings pages, built from the composition root. Dev builds say so in the title.
/// </summary>
public partial class MainWindow
{
    private Type pendingPage = typeof(MonitorPage);

    public MainWindow(AppGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        InitializeComponent();

        var title = graph.Build.IsDevBuild ? $"Ping Runner {graph.Build.Version}" : "Ping Runner";
        Title = title;
        TitleBar.Title = title;

        Navigation.SetPageProviderService(new PageProvider(new Dictionary<Type, Func<object>>
        {
            [typeof(MonitorPage)] = () => new MonitorPage(graph.Monitor),
            [typeof(GraphPage)] = () => new GraphPage(graph.Graph),
            [typeof(SpeedTestPage)] = () => new SpeedTestPage(graph.SpeedTest),
            [typeof(ConnectionPage)] = () => new ConnectionPage(graph.Connection),
            [typeof(HistoryPage)] = () => new HistoryPage(graph.History),
            [typeof(SettingsPage)] = () => new SettingsPage(graph.SettingsPage),
        }));

        graph.Navigation.Requested += (_, page) => Open(page);
        Navigation.Loaded += (_, _) => NavigateWhenReady();
    }

    public void Open(AppPage page)
    {
        pendingPage = page switch
        {
            AppPage.Graph => typeof(GraphPage),
            AppPage.SpeedTest => typeof(SpeedTestPage),
            AppPage.Connection => typeof(ConnectionPage),
            AppPage.History => typeof(HistoryPage),
            AppPage.Settings => typeof(SettingsPage),
            _ => typeof(MonitorPage),
        };
        NavigateWhenReady();
    }

    // NavigationView can only navigate once its template is applied.
    private void NavigateWhenReady()
    {
        if (Navigation.IsLoaded)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => Navigation.Navigate(pendingPage));
        }
    }
}
