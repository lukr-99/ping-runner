namespace PingRunner.Core.Tests.Fakes;

/// <summary>Collects every report at once, unlike <see cref="Progress{T}"/> which posts them later.</summary>
public sealed class SynchronousProgress<T> : IProgress<T>
{
    private readonly List<T> reports = [];

    public IReadOnlyList<T> Reports
    {
        get
        {
            lock (reports)
            {
                return [.. reports];
            }
        }
    }

    public void Report(T value)
    {
        lock (reports)
        {
            reports.Add(value);
        }
    }
}
