using System.Globalization;
using System.Reflection;
using PingRunner.Core.Pinging;
using PingRunner.Core.SpeedTest;
using PingRunner.Core.Statistics;
using PingRunner.Core.Throughput;
using PingRunner.Infrastructure.Pinging;
using PingRunner.Infrastructure.Throughput;

// pingrunner              ask for a host and ping it, then print the session's statistics
// pingrunner speed [s] [n] run a speed test for s seconds each way on n streams (defaults 10 and 4)
// pingrunner --version
var version = typeof(PingLoop).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

switch (args)
{
    case ["--version"]:
        Console.WriteLine($"Ping Runner {version}");
        return 0;
    case ["speed", .. var rest]:
        return await RunSpeedTestAsync(rest, cancellation.Token);
    case []:
        return await RunPingAsync(cancellation.Token);
    default:
        Console.Error.WriteLine("Usage: pingrunner [speed [seconds] [streams]] | --version");
        return 2;
}

static async Task<int> RunPingAsync(CancellationToken cancellationToken)
{
    var settings = AskForSettings();
    Console.WriteLine();
    Console.WriteLine($"Pinging {settings.TargetHost}. Press Ctrl+C to stop.");
    Console.WriteLine();

    var attempts = new List<PingAttempt>();
    await foreach (var attempt in new PingLoop(new IcmpPingSender(), TimeProvider.System).RunAsync(settings, cancellationToken))
    {
        attempts.Add(attempt);
        var latency = attempt.RoundtripMilliseconds is { } roundtrip ? $"{roundtrip,5} ms" : "  lost ";
        Console.WriteLine($"{attempt.Timestamp:HH:mm:ss.fff}  {latency}  {attempt.Details}");
    }

    var statistics = PingStatistics.From(attempts);
    Console.WriteLine();
    Console.WriteLine($"Sent {statistics.Sent}, received {statistics.Received}, lost {statistics.Lost} ({statistics.LossFraction:P1})");
    if (statistics.Latency is { } spread)
    {
        Console.WriteLine($"Latency  min {spread.Minimum:0.0}  avg {spread.Mean:0.0}  median {spread.Median:0.0}  p95 {spread.Percentile(95):0.0}  max {spread.Maximum:0.0} ms");
    }

    Console.WriteLine($"Jitter   {statistics.JitterMilliseconds?.ToString("0.0", CultureInfo.CurrentCulture) ?? "-"} ms   outages {statistics.Outages.Count}   call quality {statistics.CallQuality?.MeanOpinionScore.ToString("0.0", CultureInfo.CurrentCulture) ?? "-"}");
    return 0;
}

static async Task<int> RunSpeedTestAsync(string[] options, CancellationToken cancellationToken)
{
    var seconds = options.Length > 0 && int.TryParse(options[0], out var s) ? s : 10;
    var streams = options.Length > 1 && int.TryParse(options[1], out var n) ? n : 4;
    using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    var endpoint = new CloudflareSpeedEndpoint(http);
    var runner = new SpeedTestRunner(endpoint, new IcmpPingSender(), TimeProvider.System);
    var testOptions = new SpeedTestOptions { Throughput = ThroughputTestOptions.For(seconds, streams) };

    Console.WriteLine($"Speed test against {endpoint.Name}: {testOptions.Throughput.Duration.TotalSeconds:0} s each way on {testOptions.Throughput.Streams} streams.");
    var progress = new Progress<SpeedTestProgress>(report =>
    {
        if (report.Throughput is { } running)
        {
            Console.Write($"\r{report.Phase,-9} {running.Elapsed.TotalSeconds,5:0.0} s  {running.CurrentBitsPerSecond / 1e6,8:0.0} Mbps   ");
        }
    });

    try
    {
        var result = await runner.RunAsync(testOptions, progress, cancellationToken);
        Console.WriteLine();
        Console.WriteLine($"Download  {result.Download.AverageBitsPerSecond / 1e6:0.0} Mbps (peak {result.Download.PeakBitsPerSecond / 1e6:0.0})");
        Console.WriteLine($"Upload    {result.Upload.AverageBitsPerSecond / 1e6:0.0} Mbps (peak {result.Upload.PeakBitsPerSecond / 1e6:0.0})");
        Console.WriteLine($"Latency   idle {result.IdleLatency?.Median:0.0} ms, loaded down {result.DownloadLatency?.Median:0.0} ms, up {result.UploadLatency?.Median:0.0} ms");
        Console.WriteLine($"Bufferbloat {result.Bufferbloat?.GradeText ?? "-"}   data used {result.TotalBytes / 1e6:0} MB");
        return 0;
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine();
        Console.WriteLine("Cancelled.");
        return 1;
    }
    catch (Exception exception) when (exception is ThroughputTestException or HttpRequestException)
    {
        Console.WriteLine();
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

static PingRunSettings AskForSettings()
{
    while (true)
    {
        var host = Ask("Host or IP", "8.8.8.8");
        var timeout = int.TryParse(Ask("Timeout in milliseconds", "1000"), out var t) ? t : -1;
        var interval = int.TryParse(Ask("Interval in milliseconds", "1000"), out var i) ? i : -1;
        var untilStopped = Ask("Run until stopped (y/n)", "y").StartsWith('y');
        TimeSpan? duration = untilStopped
            ? null
            : TimeSpan.TryParse(Ask("Duration (hh:mm:ss)", "00:05:00"), CultureInfo.InvariantCulture, out var span) ? span : TimeSpan.Zero;

        if (PingRunSettings.TryCreate(host, timeout, interval, duration, out var error) is { } settings)
        {
            return settings;
        }

        Console.WriteLine(error);
    }
}

static string Ask(string label, string fallback)
{
    Console.Write($"{label} [{fallback}]: ");
    var input = Console.ReadLine();
    return string.IsNullOrWhiteSpace(input) ? fallback : input.Trim();
}
