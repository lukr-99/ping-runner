namespace PingRunner.Core.History;

/// <summary>The history store could not do what was asked, with a message fit to show.</summary>
public sealed class HistoryException : Exception
{
    public HistoryException()
    {
    }

    public HistoryException(string message)
        : base(message)
    {
    }

    public HistoryException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
