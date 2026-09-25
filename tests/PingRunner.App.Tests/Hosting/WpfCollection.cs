namespace PingRunner.App.Tests.Hosting;

/// <summary>Tests that touch WPF share one dispatcher thread, so they never run side by side.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WpfCollection
{
    public const string Name = "WPF";
}
