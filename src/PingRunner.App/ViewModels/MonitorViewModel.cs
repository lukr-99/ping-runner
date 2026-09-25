using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PingRunner.App.Composition;
using PingRunner.App.Desktop;
using PingRunner.App.Formatting;
using PingRunner.Core.Formatting;
using PingRunner.Core.Graphing;
using PingRunner.Core.Pinging;
using PingRunner.Core.Sessions;
using PingRunner.Core.Settings;
using PingRunner.Core.Statistics;

namespace PingRunner.App.ViewModels;

/// <summary>
/// The Monitor page: the run form, the live statistics, the last two minutes as a chart and the
/// newest pings as a list. Statistics are recomputed on <see cref="Refresh"/>, which the shell calls a
/// few times a second, so a fast interval never floods the UI.
/// </summary>
public sealed partial class MonitorViewModel : ObservableObject
{
    public const int RecentRows = 200;
    private static readonly GraphRange ChartRange = new("Last 120 pings", GraphRangeMode.RecentAttempts, RecentAttempts: 120);

    private readonly PingSession session;
    private readonly SettingsState settings;
    private readonly TimeProvider time;
    private readonly IDesktopServices desktop;
    private readonly string version;
    private bool dirty = true;

    [ObservableProperty]
    private string target;

    [ObservableProperty]
    private string intervalText;

    [ObservableProperty]
    private string timeoutText;

    [ObservableProperty]
    private DurationOption selectedDuration;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? error;

    [ObservableProperty]
    private StatisticsDisplay stats = StatisticsDisplay.Empty;

    [ObservableProperty]
    private IReadOnlyList<PingAttempt> chartAttempts = [];

    [ObservableProperty]
    private string statusText = "Ready";

    [ObservableProperty]
    private string elapsedText = Units.Clock(TimeSpan.Zero);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(StopCommand), nameof(ClearCommand))]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool isRunning;

    [ObservableProperty]
    private bool hasAttempts;

    public MonitorViewModel(PingSession session, SettingsState settings, TimeProvider time, IDesktopServices desktop, string version)
    {
        this.session = session;
        this.settings = settings;
        this.time = time;
        this.desktop = desktop;
        this.version = version;

        var current = settings.Current;
        target = current.Target;
        intervalText = current.IntervalMilliseconds.ToString(CultureInfo.CurrentCulture);
        timeoutText = current.TimeoutMilliseconds.ToString(CultureInfo.CurrentCulture);
        var stored = current.RunUntilStopped ? null : TimeSpan.TryParse(current.Duration, CultureInfo.InvariantCulture, out var span) ? span : (TimeSpan?)null;
        selectedDuration = DurationOption.For(stored);
        DurationOptions = [.. DurationOption.Presets];
        if (!DurationOptions.Contains(selectedDuration))
        {
            DurationOptions.Add(selectedDuration);
        }

        RecentTargets = [.. current.RecentTargets];
        session.AttemptRecorded += OnAttemptRecorded;
        session.StateChanged += (_, _) => OnSessionStateChanged();
    }

    public ObservableCollection<string> RecentTargets { get; }

    public bool HasError => Error is not null;

    public bool IsIdle => !IsRunning;

    public ObservableCollection<DurationOption> DurationOptions { get; }

    public ObservableCollection<AttemptRowViewModel> RecentAttempts { get; } = [];

    /// <summary>Recomputes statistics and the chart when pings arrived since the last call.</summary>
    public void Refresh()
    {
        if (session.IsRunning && session.StartedAt is { } started)
        {
            ElapsedText = Units.Clock(time.GetLocalNow() - started);
        }

        if (!dirty)
        {
            return;
        }

        dirty = false;
        var attempts = session.Snapshot();
        Stats = StatisticsDisplay.From(PingStatistics.From(attempts));
        ChartAttempts = ChartRange.Apply(attempts);
        HasAttempts = attempts.Length > 0;
    }

    /// <summary>Puts a host in the target box, for the Connection page's "Ping this" buttons.</summary>
    public void UseTarget(string host)
    {
        if (!IsRunning)
        {
            Target = host;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        Error = null;
        if (!int.TryParse(IntervalText, NumberStyles.Integer, CultureInfo.CurrentCulture, out var interval)
            || !int.TryParse(TimeoutText, NumberStyles.Integer, CultureInfo.CurrentCulture, out var timeout))
        {
            Error = "Interval and timeout are whole numbers of milliseconds.";
            return;
        }

        var run = PingRunSettings.TryCreate(Target, timeout, interval, SelectedDuration.Duration, out var problem);
        if (run is null)
        {
            Error = problem;
            return;
        }

        settings.Update(current => current.WithRecentTarget(run.TargetHost) with
        {
            IntervalMilliseconds = interval,
            TimeoutMilliseconds = timeout,
            RunUntilStopped = run.RunsUntilStopped,
            Duration = run.Duration?.ToString("c", CultureInfo.InvariantCulture) ?? current.Duration,
        });
        SyncRecentTargets(run.TargetHost);

        RecentAttempts.Clear();
        await session.RunAsync(run);
    }

    private bool CanStart() => !IsRunning;

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Stop() => session.Stop();

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Clear()
    {
        session.Clear();
        RecentAttempts.Clear();
    }

    [RelayCommand]
    private void CopySummary()
    {
        var host = session.Settings?.TargetHost ?? Target;
        desktop.CopyText(SummaryText.Build(version, host, session.Settings, PingStatistics.From(session.Snapshot())));
    }

    private void OnAttemptRecorded(object? sender, PingAttempt attempt)
    {
        dirty = true;
        RecentAttempts.Insert(0, new AttemptRowViewModel(attempt));
        if (RecentAttempts.Count > RecentRows)
        {
            RecentAttempts.RemoveAt(RecentAttempts.Count - 1);
        }
    }

    private void OnSessionStateChanged()
    {
        dirty = true;
        IsRunning = session.IsRunning;
        StatusText = session switch
        {
            { IsStopping: true } => "Stopping…",
            { IsRunning: true, Settings: { } run } => run.RunsUntilStopped
                ? $"Pinging {run.TargetHost}"
                : $"Pinging {run.TargetHost} for {Units.Span(run.Duration!.Value)}",
            { LastRunCompleted: true, Settings: { } run } => $"Finished {run.TargetHost}",
            { LastRunCompleted: false, Settings: { } run } => $"Stopped {run.TargetHost}",
            _ => "Ready",
        };

        if (!session.IsRunning && session.StartedAt is { } started && session.Attempts.Count > 0)
        {
            ElapsedText = Units.Clock(session.Attempts[^1].Timestamp - started);
        }
        else if (!session.IsRunning)
        {
            ElapsedText = Units.Clock(TimeSpan.Zero);
        }

        Refresh();
    }

    private void SyncRecentTargets(string host)
    {
        var existing = RecentTargets.FirstOrDefault(item => string.Equals(item, host, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            RecentTargets.Move(RecentTargets.IndexOf(existing), 0);
        }
        else
        {
            RecentTargets.Insert(0, host);
        }

        while (RecentTargets.Count > AppSettings.MaximumRecentTargets)
        {
            RecentTargets.RemoveAt(RecentTargets.Count - 1);
        }

        // The editable box can lose its text when the list under it moves; put it back.
        Target = host;
    }
}
