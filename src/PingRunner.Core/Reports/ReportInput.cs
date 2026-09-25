using PingRunner.Core.Network;
using PingRunner.Core.Pinging;
using PingRunner.Core.SpeedTest;

namespace PingRunner.Core.Reports;

/// <summary>Everything a report is built from.</summary>
/// <param name="Subject">What the report covers, for example "Stored run" or "Current session".</param>
/// <param name="SpeedTests">Any speed tests; only those inside the ping period are kept.</param>
public sealed record ReportInput(
    string Subject,
    IReadOnlyList<PingAttempt> Attempts,
    IReadOnlyList<SpeedTestResult> SpeedTests,
    ConnectionSnapshot? Connection,
    string? PublicIp,
    ReportOptions Options,
    DateTimeOffset GeneratedAt,
    string AppVersion);
