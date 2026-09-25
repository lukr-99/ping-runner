using PingRunner.Core.History;
using PingRunner.Core.SpeedTest;

namespace PingRunner.App.Tests.Fakes;

public sealed class InMemorySpeedTestHistory : ISpeedTestHistory
{
    private readonly object gate = new();
    private readonly Dictionary<long, SpeedTestResult> tests = [];
    private long nextId = 1;

    public int Count
    {
        get
        {
            lock (gate)
            {
                return tests.Count;
            }
        }
    }

    public Task<long> SaveAsync(SpeedTestResult result, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            var id = nextId++;
            tests[id] = result;
            return Task.FromResult(id);
        }
    }

    public Task<IReadOnlyList<SpeedTestRecord>> ListAsync(CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<SpeedTestRecord>>(
                [.. tests.OrderByDescending(entry => entry.Value.StartedAt).ThenByDescending(entry => entry.Key).Select(entry => new SpeedTestRecord(entry.Key, entry.Value))]);
        }
    }

    public Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            tests.Remove(id);
        }

        return Task.CompletedTask;
    }

    public void Clear()
    {
        lock (gate)
        {
            tests.Clear();
        }
    }
}
