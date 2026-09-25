using PingRunner.Core.Network;

namespace PingRunner.App.Tests.Fakes;

public sealed class FixedPublicIpSource(string? address) : IPublicIpSource
{
    public int Lookups { get; private set; }

    public Task<string?> GetAsync(CancellationToken cancellationToken)
    {
        Lookups++;
        return Task.FromResult(address);
    }
}
