namespace PingRunner.Core.Reports;

/// <summary>What goes into a report besides the ping figures, and how it is labelled.</summary>
public sealed record ReportOptions
{
    public const string DefaultTitle = "Connection report";

    public string Title { get; init; } = DefaultTitle;

    /// <summary>Free text shown near the top, for a ticket number, an address or what was happening.</summary>
    public string Notes { get; init; } = string.Empty;

    /// <summary>Speed tests run during the report's period.</summary>
    public bool IncludeSpeedTests { get; init; } = true;

    /// <summary>The adapter, link speed, gateway and DNS servers at the time of the report.</summary>
    public bool IncludeConnection { get; init; } = true;

    /// <summary>Off by default: the public address identifies the connection.</summary>
    public bool IncludePublicIp { get; init; }

    /// <summary>Every ping as a data sheet (Excel only; a PDF keeps to summaries).</summary>
    public bool IncludeAllPings { get; init; } = true;

    /// <summary>The heading color, "#RRGGBB".</summary>
    public string AccentColor { get; init; } = "#0F766E";
}
