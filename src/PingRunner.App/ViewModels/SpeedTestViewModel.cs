using System.Collections.ObjectModel;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PingRunner.App.Composition;
using PingRunner.App.Formatting;
using PingRunner.Core.SpeedTest;
using PingRunner.Core.Throughput;

namespace PingRunner.App.ViewModels;

/// <summary>
/// The Speed test page: runs idle latency, download and upload in turn, draws the rate live, then
/// keeps the result at the top of this session's history.
/// </summary>
public sealed partial class SpeedTestViewModel : ObservableObject
{
    private readonly SpeedTestRunner runner;
    private readonly SettingsState settings;
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

    public SpeedTestViewModel(SpeedTestRunner runner, SettingsState settings, string serverName)
    {
        this.runner = runner;
        this.settings = settings;
        ServerName = serverName;
        testDuration = TimeSpan.FromSeconds(settings.Current.SpeedTestSeconds);
    }

    public string ServerName { get; }

    public bool HasError => Error is not null;

    public ObservableCollection<SpeedTestResultViewModel> History { get; } = [];

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
            Latest = new SpeedTestResultViewModel(result);
            History.Insert(0, Latest);
            DownloadPoints = Latest.DownloadSeries;
            UploadPoints = Latest.UploadSeries;
            PhaseText = $"Finished at {Latest.When}";
            CurrentRate = Latest.DownloadAverage;
            CurrentDirection = "Mbps down";
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
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Cancel() => cancellation?.Cancel();

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
