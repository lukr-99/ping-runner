namespace PingRunner.App.Shell;

/// <summary>Lets a view model ask the main window to switch pages without knowing about it.</summary>
public sealed class ShellNavigation
{
    public event EventHandler<AppPage>? Requested;

    public void Open(AppPage page) => Requested?.Invoke(this, page);
}
