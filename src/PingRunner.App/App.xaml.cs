using System.Windows;
using PingRunner.App.Composition;
using PingRunner.App.Desktop;
using PingRunner.App.Diagnostics;
using PingRunner.App.Shell;

namespace PingRunner.App;

/// <summary>Process lifetime: the composition root, then the main window. Closing it ends the app.</summary>
public partial class App : Application
{
    private AppGraph? graph;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var build = BuildInfo.FromAssembly(typeof(App).Assembly);
        graph = new AppGraph(build, AppAdapters.ForUser(build), Resources, new WpfDesktopServices());
        CrashLog.Install(this, graph.DataFolder);

        var window = new MainWindow(graph);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        graph?.Dispose();
        base.OnExit(e);
    }
}
