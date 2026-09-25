using PingRunner.Core.Updates;

namespace PingRunner.Core.Tests.Fakes;

public sealed class StaticReleaseSource(ReleaseInfo? release, Exception? failure = null) : IReleaseSource
{
    public Task<ReleaseInfo?> GetLatestAsync(CancellationToken cancellationToken) =>
        failure is null ? Task.FromResult(release) : Task.FromException<ReleaseInfo?>(failure);
}
