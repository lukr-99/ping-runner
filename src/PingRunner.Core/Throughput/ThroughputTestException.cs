namespace PingRunner.Core.Throughput;

/// <summary>A throughput test that could not move any data, with a message fit for the result card.</summary>
public sealed class ThroughputTestException : Exception
{
    public ThroughputTestException()
    {
    }

    public ThroughputTestException(string message)
        : base(message)
    {
    }

    public ThroughputTestException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
