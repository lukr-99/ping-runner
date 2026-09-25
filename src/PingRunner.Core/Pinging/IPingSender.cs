namespace PingRunner.Core.Pinging;

/// <summary>
/// Sends one echo request. The ICMP adapter lives in Infrastructure; tests use a scripted fake so the
/// loop and the statistics never depend on the network.
/// </summary>
public interface IPingSender
{
    /// <summary>
    /// Sends one request and reports the outcome. Network failures come back as a failed outcome;
    /// only cancellation throws.
    /// </summary>
    Task<PingOutcome> SendAsync(string host, TimeSpan timeout, CancellationToken cancellationToken);
}
