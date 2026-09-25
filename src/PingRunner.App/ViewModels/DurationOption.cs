using PingRunner.App.Formatting;

namespace PingRunner.App.ViewModels;

/// <summary>A run length in the Monitor's duration list; null runs until stopped.</summary>
public sealed record DurationOption(string Label, TimeSpan? Duration)
{
    public static IReadOnlyList<DurationOption> Presets { get; } =
    [
        new("Until stopped", null),
        new("1 minute", TimeSpan.FromMinutes(1)),
        new("5 minutes", TimeSpan.FromMinutes(5)),
        new("15 minutes", TimeSpan.FromMinutes(15)),
        new("30 minutes", TimeSpan.FromMinutes(30)),
        new("1 hour", TimeSpan.FromHours(1)),
        new("4 hours", TimeSpan.FromHours(4)),
        new("8 hours", TimeSpan.FromHours(8)),
        new("24 hours", TimeSpan.FromHours(24)),
    ];

    /// <summary>The preset for a stored length, or a new option naming it when no preset matches.</summary>
    public static DurationOption For(TimeSpan? duration) =>
        Presets.FirstOrDefault(option => option.Duration == duration)
        ?? new DurationOption(Units.Span(duration ?? TimeSpan.Zero), duration);

    public override string ToString() => Label;
}
