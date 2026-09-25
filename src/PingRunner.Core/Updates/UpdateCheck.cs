namespace PingRunner.Core.Updates;

/// <summary>
/// Compares the running version with the newest release. It only discovers: installing stays a manual
/// step on the release page, so nothing is downloaded or run without the user choosing it.
/// </summary>
public sealed class UpdateCheck(IReleaseSource source, ReleaseVersion current)
{
    public ReleaseVersion Current { get; } = current;

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            var latest = await source.GetLatestAsync(cancellationToken).ConfigureAwait(false);
            return latest switch
            {
                null => new UpdateCheckResult(UpdateStatus.NoReleases, null),
                _ when latest.Version > Current => new UpdateCheckResult(UpdateStatus.UpdateAvailable, latest),
                _ => new UpdateCheckResult(UpdateStatus.UpToDate, latest),
            };
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or InvalidDataException
            && !cancellationToken.IsCancellationRequested)
        {
            return new UpdateCheckResult(UpdateStatus.Failed, null, exception.Message);
        }
    }
}
