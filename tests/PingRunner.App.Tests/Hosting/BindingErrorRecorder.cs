using System.Diagnostics;
using System.Windows;

namespace PingRunner.App.Tests.Hosting;

/// <summary>
/// Collects WPF data-binding errors while it is alive, so a mistyped binding path in a page fails a
/// test instead of silently showing nothing.
/// </summary>
public sealed class BindingErrorRecorder : TraceListener
{
    private readonly List<string> errors = [];

    public BindingErrorRecorder()
    {
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        PresentationTraceSources.DataBindingSource.Listeners.Add(this);
    }

    public IReadOnlyList<string> Errors
    {
        get
        {
            lock (errors)
            {
                return [.. errors];
            }
        }
    }

    public override void Write(string? message)
    {
    }

    public override void WriteLine(string? message)
    {
        if (message is null)
        {
            return;
        }

        lock (errors)
        {
            errors.Add(message);
        }
    }

    protected override void Dispose(bool disposing)
    {
        PresentationTraceSources.DataBindingSource.Listeners.Remove(this);
        base.Dispose(disposing);
    }
}
