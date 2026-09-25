using System.Windows;
using System.Windows.Threading;
using PingRunner.App.Desktop;
using PingRunner.App.Shell;
using PingRunner.App.Theming;
using PingRunner.App.ViewModels;
using PingRunner.Core.History;
using PingRunner.Core.Pinging;
using PingRunner.Core.Sessions;
using PingRunner.Core.SpeedTest;
using PingRunner.Core.Updates;

namespace PingRunner.App.Composition;

/// <summary>
/// The one composition root: every view model is built here from the adapters and handed on through
/// constructors. It also owns the UI refresh tick that lets the Monitor and Graph pages redraw a few
/// times a second however fast pings arrive. Lives as long as the process.
/// </summary>
public sealed class AppGraph : IDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(250);

    private readonly AppAdapters adapters;
    private readonly DispatcherTimer refresh;

    public AppGraph(BuildInfo build, AppAdapters adapters, ResourceDictionary resources, IDesktopServices desktop)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(adapters);
        this.adapters = adapters;
        Build = build;
        Settings = new SettingsState(adapters.SettingsStore);
        Theme = new ThemeApplier(resources);
        Theme.Apply(Settings.Current.Theme, Settings.Current.Accent);
        Navigation = new ShellNavigation();

        var time = adapters.Time;
        Session = new PingSession(new PingLoop(adapters.Pinger, time), time) { Capacity = Settings.Current.MaximumStoredAttempts };
        HistoryChanges = new HistoryChanges();
        Recorder = new RunRecorder(Session, adapters.PingRuns, time);

        Monitor = new MonitorViewModel(Session, Settings, time, desktop, build.Version.ToString());
        Graph = new GraphViewModel(Session, desktop);
        SpeedTest = new SpeedTestViewModel(
            new SpeedTestRunner(adapters.SpeedEndpoint, adapters.Pinger, time), Settings, adapters.SpeedEndpoint.Name, adapters.SpeedTests, HistoryChanges);
        History = new HistoryViewModel(adapters.PingRuns, adapters.SpeedTests, adapters.History, Graph, Navigation, desktop, HistoryChanges);
        Connection = new ConnectionViewModel(adapters.Connections, adapters.PublicIp, Monitor, Navigation, desktop);
        SettingsPage = new SettingsViewModel(
            Settings, Theme, Session, new UpdateCheck(adapters.Releases, build.Version), build, adapters.DataFolder, desktop, adapters.History, HistoryChanges);

        // Every run lands in the history; runs a crash left open are closed first.
        Recorder.RunSaved += (_, _) => HistoryChanges.Raise();
        Recorder.Failed += (_, exception) => History.ReportProblem($"A ping run could not be saved to the history: {exception.Message}");
        Recorder.RecoverInterruptedRuns();
        _ = SpeedTest.LoadAsync();

        refresh = new DispatcherTimer(DispatcherPriority.Background) { Interval = RefreshInterval };
        refresh.Tick += (_, _) => RefreshPages();
        refresh.Start();
    }

    public BuildInfo Build { get; }

    public string DataFolder => adapters.DataFolder;

    public SettingsState Settings { get; }

    public ThemeApplier Theme { get; }

    public ShellNavigation Navigation { get; }

    public PingSession Session { get; }

    public RunRecorder Recorder { get; }

    public HistoryChanges HistoryChanges { get; }

    public HistoryViewModel History { get; }

    public MonitorViewModel Monitor { get; }

    public GraphViewModel Graph { get; }

    public SpeedTestViewModel SpeedTest { get; }

    public ConnectionViewModel Connection { get; }

    public SettingsViewModel SettingsPage { get; }

    /// <summary>What the refresh tick does; callable directly where no tick runs.</summary>
    public void RefreshPages()
    {
        Monitor.Refresh();
        Graph.Refresh();
    }

    public void Dispose()
    {
        refresh.Stop();
        Recorder.Close(TimeSpan.FromSeconds(3));
        Session.Stop();
        Theme.Dispose();
        adapters.Dispose();
    }
}
