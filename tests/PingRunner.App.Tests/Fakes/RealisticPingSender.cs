using PingRunner.Core.Pinging;

namespace PingRunner.App.Tests.Fakes;

/// <summary>
/// Replies like a decent home line: around 18 ms with a little noise, an occasional spike, a lost
/// ping now and then, and one short outage. Seeded, so every run gives the same series.
/// </summary>
public sealed class RealisticPingSender(int seed = 7) : IPingSender
{
    private readonly Random random = new(seed);
    private int sent;

    public Task<PingOutcome> SendAsync(string host, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var index = sent++;
#pragma warning disable CA5394 // Test data, not security.
        var roll = random.NextDouble();
        if (index is >= 140 and < 144 || roll < 0.006)
        {
            return Task.FromResult(PingOutcome.Failure("Request timed out"));
        }

        var latency = 16 + (random.NextDouble() * 5) + (roll > 0.97 ? 25 + (random.NextDouble() * 40) : 0);
#pragma warning restore CA5394
        return Task.FromResult(PingOutcome.Reply((long)Math.Round(latency), host));
    }
}
