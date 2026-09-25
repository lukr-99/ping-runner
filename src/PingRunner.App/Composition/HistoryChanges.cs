namespace PingRunner.App.Composition;

/// <summary>
/// Tells the pages that show stored measurements that the history changed: a run or speed test was
/// saved, something was deleted, or a backup was restored. Raised and handled on the UI thread.
/// </summary>
public sealed class HistoryChanges
{
    public event EventHandler? Changed;

    public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}
