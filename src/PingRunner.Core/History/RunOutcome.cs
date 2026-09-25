namespace PingRunner.Core.History;

/// <summary>How a stored ping run ended.</summary>
public enum RunOutcome
{
    /// <summary>Still going, or the app closed before it could say otherwise.</summary>
    Running,

    /// <summary>Ran its full duration.</summary>
    Completed,

    /// <summary>Stopped by the user or by closing the app.</summary>
    Stopped,

    /// <summary>Found still marked running at the next start: the app or the computer went down mid-run.</summary>
    Interrupted,
}
