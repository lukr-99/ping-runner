using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using PingRunner.Core.Pinging.Models;

namespace PingRunner.Core.Pinging.Services;

public sealed class PingService
{
    public async IAsyncEnumerable<PingAttemptResult> RunAsync(
        PingRunSettings settings,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var ping = new Ping();
        var startedAt = DateTimeOffset.UtcNow;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (!settings.RunForever &&
                settings.RunDuration is { } runDuration &&
                DateTimeOffset.UtcNow - startedAt >= runDuration)
            {
                yield break;
            }

            yield return await SendAttemptAsync(ping, settings, cancellationToken);

            if (!settings.RunForever &&
                settings.RunDuration is { } updatedRunDuration &&
                DateTimeOffset.UtcNow - startedAt >= updatedRunDuration)
            {
                yield break;
            }

            try
            {
                await Task.Delay(settings.IntervalMilliseconds, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
        }
    }

    private static async Task<PingAttemptResult> SendAttemptAsync(
        Ping ping,
        PingRunSettings settings,
        CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.Now;

        try
        {
            var reply = await ping.SendPingAsync(
                settings.TargetHost,
                TimeSpan.FromMilliseconds(settings.TimeoutMilliseconds),
                [],
                new PingOptions(),
                cancellationToken);
            var isSuccess = reply.Status == IPStatus.Success;
            var details = isSuccess
                ? $"Reply from {reply.Address}"
                : $"Ping status: {reply.Status}";

            return new PingAttemptResult(
                settings.TargetHost,
                timestamp,
                isSuccess,
                isSuccess ? reply.RoundtripTime : null,
                details);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is PingException or InvalidOperationException or ArgumentException)
        {
            return new PingAttemptResult(
                settings.TargetHost,
                timestamp,
                false,
                null,
                exception.Message);
        }
    }
}
