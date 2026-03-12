using PingerTool.Core.Pinging.Models;
using PingerTool.Core.Pinging.Services;

var settings = BuildSettingsFromConsole();
var pingService = new PingService();

using var cancellationTokenSource = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationTokenSource.Cancel();
};

Console.WriteLine();
Console.WriteLine($"Starting ping run for {settings.TargetHost}");
Console.WriteLine("Press Ctrl+C to stop.");
Console.WriteLine();

var sentCount = 0;
var successCount = 0;
var totalLatencyMilliseconds = 0L;
var latencySamples = 0;

await foreach (var attempt in pingService.RunAsync(settings, cancellationTokenSource.Token))
{
    sentCount++;

    if (attempt.IsSuccess)
    {
        successCount++;
    }

    if (attempt.RoundtripTimeMilliseconds is { } roundtripTimeMilliseconds)
    {
        totalLatencyMilliseconds += roundtripTimeMilliseconds;
        latencySamples++;
    }

    var latencyText = attempt.RoundtripTimeMilliseconds?.ToString() ?? "-";
    Console.WriteLine(
        $"{attempt.TimestampDisplay} | {attempt.OutcomeDisplay,-7} | {latencyText,4} ms | {attempt.Details}");
}

var averageLatencyText = latencySamples == 0
    ? "-"
    : $"{Math.Round((double)totalLatencyMilliseconds / latencySamples, 1):0.0} ms";
var successRateText = sentCount == 0
    ? "-"
    : $"{(double)successCount / sentCount:P1}";

Console.WriteLine();
Console.WriteLine("Summary");
Console.WriteLine($"Sent: {sentCount}");
Console.WriteLine($"Success: {successCount}");
Console.WriteLine($"Failure: {sentCount - successCount}");
Console.WriteLine($"Average latency: {averageLatencyText}");
Console.WriteLine($"Success rate: {successRateText}");

static PingRunSettings BuildSettingsFromConsole()
{
    var targetHost = ReadRequiredString("Host or IP", "8.8.8.8");
    var timeoutMilliseconds = ReadPositiveInt("Timeout in milliseconds", 1000);
    var intervalMilliseconds = ReadPositiveInt("Interval in milliseconds", 1000);
    var runForever = ReadYesNo("Run until stopped", true);

    if (runForever)
    {
        return new PingRunSettings(targetHost, timeoutMilliseconds, intervalMilliseconds, null, true);
    }

    var duration = ReadPositiveTimeSpan("Duration (hh:mm:ss)", "00:05:00");
    return new PingRunSettings(targetHost, timeoutMilliseconds, intervalMilliseconds, duration, false);
}

static string ReadRequiredString(string label, string defaultValue)
{
    while (true)
    {
        Console.Write($"{label} [{defaultValue}]: ");
        var input = Console.ReadLine();
        var value = string.IsNullOrWhiteSpace(input) ? defaultValue : input.Trim();

        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }
    }
}

static int ReadPositiveInt(string label, int defaultValue)
{
    while (true)
    {
        Console.Write($"{label} [{defaultValue}]: ");
        var input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input))
        {
            return defaultValue;
        }

        if (int.TryParse(input, out var value) && value > 0)
        {
            return value;
        }

        Console.WriteLine("Enter a positive integer.");
    }
}

static bool ReadYesNo(string label, bool defaultValue)
{
    var defaultLabel = defaultValue ? "Y/n" : "y/N";

    while (true)
    {
        Console.Write($"{label} [{defaultLabel}]: ");
        var input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input))
        {
            return defaultValue;
        }

        if (input.Equals("y", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("yes", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (input.Equals("n", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("no", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Console.WriteLine("Enter y or n.");
    }
}

static TimeSpan ReadPositiveTimeSpan(string label, string defaultValue)
{
    while (true)
    {
        Console.Write($"{label} [{defaultValue}]: ");
        var input = Console.ReadLine();
        var value = string.IsNullOrWhiteSpace(input) ? defaultValue : input.Trim();

        if (TimeSpan.TryParse(value, out var duration) && duration > TimeSpan.Zero)
        {
            return duration;
        }

        Console.WriteLine("Enter a positive time span such as 00:30:00.");
    }
}
