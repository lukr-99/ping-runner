namespace PingRunner.Core.SpeedTest;

/// <summary>
/// Forwards each report at once on the reporting thread. The outer progress the caller passed in
/// decides where reports land (a UI's <see cref="Progress{T}"/> posts to its dispatcher), so this
/// inner step must not post a second time.
/// </summary>
internal sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}
