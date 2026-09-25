namespace PingRunner.App.Reports;

/// <summary>What a report covers.</summary>
public enum ReportSourceKind
{
    /// <summary>The pings of the session running or last run on the Monitor page.</summary>
    LiveSession,

    /// <summary>Whatever the Graph page shows: an imported file or a run opened from the history.</summary>
    GraphView,

    /// <summary>One run from the history.</summary>
    StoredRun,

    /// <summary>Every stored ping to one target between two dates, across runs.</summary>
    HistoryPeriod,
}
