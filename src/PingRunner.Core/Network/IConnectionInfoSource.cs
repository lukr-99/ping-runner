namespace PingRunner.Core.Network;

public interface IConnectionInfoSource
{
    /// <summary>The adapter with the default route, or null when the computer is offline.</summary>
    ConnectionSnapshot? GetPrimary();
}
