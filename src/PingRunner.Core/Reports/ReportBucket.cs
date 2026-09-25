namespace PingRunner.Core.Reports;

/// <summary>One slice of the report's period (a minute to a day, depending on its length) and how it went.</summary>
public sealed record ReportBucket(
    DateTimeOffset Start,
    DateTimeOffset End,
    int Sent,
    int Received,
    double? MeanMilliseconds,
    double? P95Milliseconds,
    double? MaximumMilliseconds,
    int OutagesStarted)
{
    public int Lost => Sent - Received;

    public double? LossFraction => Sent == 0 ? null : (double)Lost / Sent;
}
