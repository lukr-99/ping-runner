using System.Globalization;
using PingRunner.App.Formatting;
using PingRunner.Core.SpeedTest;
using PingRunner.Core.Throughput;

namespace PingRunner.App.ViewModels;

/// <summary>A finished speed test written out for the result cards and the history lists.</summary>
public sealed class SpeedTestResultViewModel(SpeedTestResult result, long? id = null)
{
    /// <summary>The stored test's id; null when it could not be stored.</summary>
    public long? Id { get; } = id;

    public SpeedTestResult Result { get; } = result;

    public string When { get; } = result.StartedAt.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture);

    public string WhenLong { get; } = result.StartedAt.ToLocalTime().ToString("ddd d MMM yyyy, HH:mm", CultureInfo.CurrentCulture);

    public string DownloadAverage { get; } = Units.MegabitsNumber(result.Download.AverageBitsPerSecond);

    public string DownloadPeak { get; } = $"peak {Units.BitsPerSecond(result.Download.PeakBitsPerSecond)}";

    public string UploadAverage { get; } = Units.MegabitsNumber(result.Upload.AverageBitsPerSecond);

    public string UploadPeak { get; } = $"peak {Units.BitsPerSecond(result.Upload.PeakBitsPerSecond)}";

    public string DownloadShort { get; } = Units.BitsPerSecond(result.Download.AverageBitsPerSecond);

    public string UploadShort { get; } = Units.BitsPerSecond(result.Upload.AverageBitsPerSecond);

    public string IdleLatency { get; } = Units.Milliseconds(result.IdleLatency?.Median);

    public string IdleLatencyHint { get; } = result.IdleLatency is { } idle
        ? $"median to {result.LatencyHost} · best {Units.Milliseconds(idle.Minimum)}"
        : $"{result.LatencyHost} did not answer ping";

    /// <summary>"42 / 57 ms": the median while downloading, then while uploading.</summary>
    public string LoadedLatency { get; } =
        $"{Units.Milliseconds(result.DownloadLatency?.Median).Replace(" ms", string.Empty, StringComparison.Ordinal)} / {Units.Milliseconds(result.UploadLatency?.Median)}";

    public string Grade { get; } = result.Bufferbloat?.GradeText ?? Units.None;

    public string GradeHint { get; } = result.Bufferbloat is { } bloat
        ? $"+{Units.Milliseconds(bloat.IncreaseMilliseconds)} under load"
        : "Needs ping replies to grade";

    public Severity GradeSeverity { get; } = result.Bufferbloat?.Grade switch
    {
        null => Severity.Neutral,
        BufferbloatGrade.APlus or BufferbloatGrade.A => Severity.Good,
        BufferbloatGrade.B => Severity.Neutral,
        BufferbloatGrade.C => Severity.Warning,
        _ => Severity.Bad,
    };

    public string DataUsed { get; } = Units.Bytes(result.TotalBytes);

    public IReadOnlyList<ThroughputPoint> DownloadSeries { get; } = result.Download.Series;

    public IReadOnlyList<ThroughputPoint> UploadSeries { get; } = result.Upload.Series;
}
