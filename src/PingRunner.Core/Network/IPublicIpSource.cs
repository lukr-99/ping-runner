namespace PingRunner.Core.Network;

/// <summary>Asks an outside service which address the internet sees this computer at.</summary>
public interface IPublicIpSource
{
    /// <summary>Null when no service answered with an address.</summary>
    Task<string?> GetAsync(CancellationToken cancellationToken);
}
