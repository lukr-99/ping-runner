using PingRunner.Core.History;

namespace PingRunner.App.Tests.Fakes;

/// <summary>Counts what the in-memory stores hold; backup and restore only record what was asked.</summary>
public sealed class FakeHistoryMaintenance(InMemoryPingRunHistory runs, InMemorySpeedTestHistory speedTests) : IHistoryMaintenance
{
    public string Location => @"C:\Users\you\AppData\Local\PingRunner\history.db";

    public string? Problem { get; set; }

    public List<string> BackedUpTo { get; } = [];

    public List<string> RestoredFrom { get; } = [];

    public async Task<HistoryStorageInfo> GetInfoAsync(CancellationToken cancellationToken)
    {
        var stored = await runs.ListRunsAsync(cancellationToken);
        var attempts = stored.Sum(run => (long)runs.StoredAttempts(run.Id));
        return new HistoryStorageInfo(stored.Count, attempts, speedTests.Count, 1_234_567);
    }

    public Task BackupAsync(string destinationPath, CancellationToken cancellationToken)
    {
        BackedUpTo.Add(destinationPath);
        return Task.CompletedTask;
    }

    public async Task<HistoryStorageInfo> RestoreAsync(string sourcePath, CancellationToken cancellationToken)
    {
        RestoredFrom.Add(sourcePath);
        return await GetInfoAsync(cancellationToken);
    }

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        foreach (var run in await runs.ListRunsAsync(cancellationToken))
        {
            await runs.DeleteRunAsync(run.Id, cancellationToken);
        }

        speedTests.Clear();
    }
}
