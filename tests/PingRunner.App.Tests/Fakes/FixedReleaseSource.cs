using PingRunner.Core.Updates;

namespace PingRunner.App.Tests.Fakes;

public sealed class FixedReleaseSource(ReleaseInfo? release) : IReleaseSource
{
    public Task<ReleaseInfo?> GetLatestAsync(CancellationToken cancellationToken) => Task.FromResult(release);
}
