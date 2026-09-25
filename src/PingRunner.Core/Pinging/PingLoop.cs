using System.Runtime.CompilerServices;

namespace PingRunner.Core.Pinging;

/// <summary>
/// Sends pings on a fixed cadence: each request is due one interval after the previous one was due, so
/// a slow reply does not stretch the schedule. When a reply takes longer than the interval the next
/// request goes out at once and the schedule restarts from there, without a burst to catch up.
/// </summary>
public sealed class PingLoop(IPingSender sender, TimeProvider time)
{
    public async IAsyncEnumerable<PingAttempt> RunAsync(
        PingRunSettings settings,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var startedAt = time.GetUtcNow();
        var due = startedAt;

        while (!cancellationToken.IsCancellationRequested && !IsOver(settings, startedAt))
        {
            var attempt = await SendAsync(settings, cancellationToken).ConfigureAwait(false);
            if (attempt is null)
            {
                yield break;
            }

            yield return attempt;

            due += settings.Interval;
            var now = time.GetUtcNow();
            if (due < now)
            {
                due = now;
            }

            if (settings.Duration is { } duration && due - startedAt >= duration)
            {
                yield break;
            }

            if (!await DelayAsync(due - now, cancellationToken).ConfigureAwait(false))
            {
                yield break;
            }
        }
    }

    private bool IsOver(PingRunSettings settings, DateTimeOffset startedAt) =>
        settings.Duration is { } duration && time.GetUtcNow() - startedAt >= duration;

    private async Task<PingAttempt?> SendAsync(PingRunSettings settings, CancellationToken cancellationToken)
    {
        var timestamp = time.GetLocalNow();
        try
        {
            var outcome = await sender.SendAsync(settings.TargetHost, settings.Timeout, cancellationToken).ConfigureAwait(false);
            return new PingAttempt(
                settings.TargetHost,
                timestamp,
                outcome.IsSuccess,
                outcome.IsSuccess ? outcome.RoundtripMilliseconds : null,
                outcome.Details);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private async Task<bool> DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (delay <= TimeSpan.Zero)
        {
            return !cancellationToken.IsCancellationRequested;
        }

        try
        {
            await Task.Delay(delay, time, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
