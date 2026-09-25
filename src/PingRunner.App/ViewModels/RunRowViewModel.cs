using System.Globalization;
using PingRunner.App.Formatting;
using PingRunner.Core.History;
using PingRunner.Core.Statistics;

namespace PingRunner.App.ViewModels;

/// <summary>One stored ping run as a line of the History list.</summary>
public sealed class RunRowViewModel(RunRecord run)
{
    public long Id { get; } = run.Id;

    public string Target { get; } = run.TargetHost;

    public string When { get; } = run.StartedAt.ToLocalTime().ToString("ddd d MMM yyyy, HH:mm", CultureInfo.CurrentCulture);

    public string Duration { get; } = run.Outcome == RunOutcome.Running || run.Duration is not { } span ? "–" : Units.Span(span);

    /// <summary>How the run ended, or "Running now" while it goes on.</summary>
    public string Status { get; } = run.Outcome switch
    {
        RunOutcome.Completed => "Completed",
        RunOutcome.Stopped => "Stopped",
        RunOutcome.Interrupted => "Interrupted",
        _ => "Running now",
    };

    public string Pings { get; } = Units.Count(run.Summary.Sent);

    public string Loss { get; } = Units.Percent(run.Summary.LossFraction);

    public Severity LossSeverity { get; } = SeverityRules.Loss(run.Summary.LossFraction);

    public string Average { get; } = Units.Milliseconds(run.Summary.MeanMilliseconds);

    public string Jitter { get; } = Units.Milliseconds(run.Summary.JitterMilliseconds);

    public string Outages { get; } = Units.Count(run.Summary.Outages);

    public string OutagesHint { get; } = run.Summary.LongestOutage is { } longest ? $"Longest {Units.Span(longest)}" : "No outages";

    public string Quality { get; } = run.Summary.MeanOpinionScore is { } mos ? mos.ToString("0.0", CultureInfo.CurrentCulture) : "–";

    public Severity QualitySeverity { get; } = SeverityRules.Quality(
        run.Summary.MeanOpinionScore is { } score ? CallQuality.RatingOf(score) : null);

    public bool IsRunning { get; } = run.Outcome == RunOutcome.Running;

    public bool IsFinished => !IsRunning;
}
