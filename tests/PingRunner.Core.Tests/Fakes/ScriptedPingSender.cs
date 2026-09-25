using PingRunner.Core.Pinging;

namespace PingRunner.Core.Tests.Fakes;

/// <summary>Answers with the scripted outcomes in order, then repeats the last one; records every call.</summary>
public sealed class ScriptedPingSender(params PingOutcome[] outcomes) : IPingSender
{
    private int next;

    public List<(string Host, TimeSpan Timeout)> Calls { get; } = [];

    public Task<PingOutcome> SendAsync(string host, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (Calls)
        {
            Calls.Add((host, timeout));
            var outcome = outcomes[Math.Min(next, outcomes.Length - 1)];
            next++;
            return Task.FromResult(outcome);
        }
    }
}
