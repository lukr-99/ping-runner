using System.Collections.ObjectModel;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PingRunner.App.Composition;
using PingRunner.Core.Formatting;
using PingRunner.Core.History;
using PingRunner.Core.SpeedTest;
using PingRunner.Core.Throughput;

namespace PingRunner.App.ViewModels;

/// <summary>
/// The Speed test page: runs idle latency, download and upload in turn, draws the rate live, then
/// stores the result in the history and keeps the most recent ones on the page.
/// </summary>
public sealed partial class SpeedTestViewModel : ObservableObject
{
    public const int RecentCount = 20;

    private readonly SpeedTestRunner runner;
    private readonly SettingsState settings;
    private readonly ISpeedTestHistory history;
    private readonly HistoryChanges changes;
    private CancellationTokenSource? cancellation;
    private List<ThroughputPoint> download = [];
    private List<ThroughputPoint> upload = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool isRunning;

    [ObservableProperty]
    private string phaseText = "Measures latency, then download and upload, each for a few seconds.";

    [ObservableProperty]
    private double progress;

    [ObservableProperty]
    private string currentRate = Units.None;

    [ObservableProperty]
    private string currentDirection = "Mbps";

    [ObservableProperty]
    private IReadOnlyList<ThroughputPoint> downloadPoints = [];

    [ObservableProperty]
    private IReadOnlyList<ThroughputPoint> uploadPoints = [];

    [ObservableProperty]
    private TimeSpan testDuration;

    [ObservableProperty]
    private SpeedTestResultViewModel? latest;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? error;

    public SpeedTestViewModel(SpeedTestRunner runner, SettingsState settings, string serverName, ISpeedTestHistory history, HistoryChanges changes)
    {
        this.runner = runner;
        this.settings = settings;
        this.history = history;
        this.changes = changes;
        ServerName = serverName;
        testDuration = TimeSpan.FromSeconds(settings.Current.SpeedTestSeconds);
        changes.Changed += async (_, _) =>
        {
            if (!IsRunning)
            {
                await LoadAsync().ConfigureAwait(true);
            }
        };
    }

    public string ServerName { get; }

    public bool HasError => Error is not null;

    /// <summary>The most recent stored tests, newest first.</summary>
    public ObservableCollection<SpeedTestResultViewModel> History { get; } = [];

    /// <summary>Shows the stored tests, the latest one in the result cards and the chart.</summary>
    public async Task LoadAsync()
    {
        IReadOnlyList<SpeedTestRecord> stored;
        try
        {
            stored = await history.ListAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (HistoryException)
        {
            // The History page reports the problem; this page just starts empty.
            return;
        }

        History.Clear();
        foreach (var record in stored.Take(RecentCount))
        {
            History.Add(new SpeedTestResultViewModel(record.Result, record.Id));
        }

        if (!IsRunning)
        {
            ShowLatest(History.FirstOrDefault());
        }
    }

    public string DataHint => $"Each direction runs {settings.Current.SpeedTestSeconds} s on {settings.Current.SpeedTestStreams} parallel streams; a fast line can move several hundred MB.";

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task RunAsync()
    {
        var current = settings.Current;
        var options = new SpeedTestOptions { Throughput = ThroughputTestOptions.For(current.SpeedTestSeconds, current.SpeedTestStreams) };
        TestDuration = options.Throughput.Duration;
        OnPropertyChanged(nameof(DataHint));
        download = [];
        upload = [];
        DownloadPoints = [];
        UploadPoints = [];
        Error = null;
        Progress = 0;
        CurrentRate = Units.None;
        IsRunning = true;
        cancellation = new CancellationTokenSource();

        try
        {
            var result = await runner.RunAsync(options, new Progress<SpeedTestProgress>(report => OnProgress(report, options)), cancellation.Token)
                .ConfigureAwait(true);
            long? id = null;
            try
            {
                id = await history.SaveAsync(result, CancellationToken.None).ConfigureAwait(true);
            }
            catch (HistoryException exception)
            {
                Error = $"The result could not be saved to the history: {exception.Message}";
            }

            var finished = new SpeedTestResultViewModel(result, id);
            History.Insert(0, finished);
            while (History.Count > RecentCount)
            {
                History.RemoveAt(History.Count - 1);
            }

            ShowLatest(finished);
            PhaseText = $"Finished at {finished.When}";
            Progress = 1;
        }
        catch (OperationCanceledException)
        {
            PhaseText = "Cancelled";
            CurrentRate = Units.None;
        }
        catch (Exception exception) when (exception is ThroughputTestException or HttpRequestException)
        {
            Error = exception.Message;
            PhaseText = "The test could not finish";
            CurrentRate = Units.None;
        }
        finally
        {
            cancellation.Dispose();
            cancellation = null;
            IsRunning = false;
        }

        changes.Raise();
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Cancel() => cancellation?.Cancel();

    private void ShowLatest(SpeedTestResultViewModel? result)
    {
        Latest = result;
        if (result is null)
        {
            return;
        }

        TestDuration = result.Result.Download.Duration > TimeSpan.Zero ? result.Result.Download.Duration : TestDuration;
        DownloadPoints = result.DownloadSeries;
        UploadPoints = result.UploadSeries;
        CurrentRate = result.DownloadAverage;
        CurrentDirection = "Mbps down";
        PhaseText = $"Last test: {result.WhenLong}";
        Progress = 1;
    }

    private void OnProgress(SpeedTestProgress report, SpeedTestOptions options)
    {
        if (!IsRunning)
        {
            return;
        }

        var seconds = options.Throughput.Duration.TotalSeconds;
        switch (report.Phase)
        {
            case SpeedTestPhase.IdleLatency:
                PhaseText = report.LatestLatencyMilliseconds is { } latency
                    ? $"Measuring idle latency · {Units.Milliseconds(latency)}"
                    : "Measuring idle latency";
                Progress = 0.04;
                break;
            case SpeedTestPhase.Download when report.Throughput is { } running:
                download.Add(new ThroughputPoint(running.Elapsed, running.CurrentBitsPerSecond));
                DownloadPoints = [.. download];
                PhaseText = $"Download · {Math.Max(0, seconds - running.Elapsed.TotalSeconds):0} s left";
                CurrentRate = Units.MegabitsNumber(running.CurrentBitsPerSecond);
                CurrentDirection = "Mbps down";
                Progress = 0.05 + (0.475 * running.Elapsed.TotalSeconds / seconds);
                break;
            case SpeedTestPhase.Upload when report.Throughput is { } running:
                upload.Add(new ThroughputPoint(running.Elapsed, running.CurrentBitsPerSecond));
                UploadPoints = [.. upload];
                PhaseText = $"Upload · {Math.Max(0, seconds - running.Elapsed.TotalSeconds):0} s left";
                CurrentRate = Units.MegabitsNumber(running.CurrentBitsPerSecond);
                CurrentDirection = "Mbps up";
                Progress = 0.525 + (0.475 * running.Elapsed.TotalSeconds / seconds);
                break;
        }
    }
}
