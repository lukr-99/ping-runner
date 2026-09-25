using PingRunner.Core.SpeedTest;

namespace PingRunner.Core.History;

/// <summary>Stored speed tests. Failures throw <see cref="HistoryException"/>.</summary>
public interface ISpeedTestHistory
{
    /// <returns>The stored test's id.</returns>
    Task<long> SaveAsync(SpeedTestResult result, CancellationToken cancellationToken);

    /// <summary>Every stored test, newest first.</summary>
    Task<IReadOnlyList<SpeedTestRecord>> ListAsync(CancellationToken cancellationToken);

    Task DeleteAsync(long id, CancellationToken cancellationToken);
}
