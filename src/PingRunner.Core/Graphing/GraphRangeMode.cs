namespace PingRunner.Core.Graphing;

public enum GraphRangeMode
{
    /// <summary>The last N attempts.</summary>
    RecentAttempts,

    /// <summary>Attempts within a time span before the newest one.</summary>
    RollingTime,

    /// <summary>Everything in the source.</summary>
    All,
}
