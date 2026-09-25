using System.Globalization;
using PingRunner.Core.Formatting;
using PingRunner.Core.History;

namespace PingRunner.App.Reports;

/// <summary>A stored run as an entry of the report source list.</summary>
public sealed class ReportRunChoice(RunRecord run)
{
    public RunRecord Run { get; } = run;

    public long Id => Run.Id;

    public string Label { get; } = string.Join(
        "  ·  ",
        new[]
        {
            run.StartedAt.ToString("ddd d MMM yyyy, HH:mm", CultureInfo.CurrentCulture),
            run.TargetHost,
            run.Duration is { } duration ? Units.Span(duration) : "running",
            $"{Units.Percent(run.Summary.LossFraction)} lost",
            run.Source == RunSource.Imported ? $"from {run.SourceName}" : null,
        }.OfType<string>());

    public override string ToString() => Label;
}
