namespace PingRunner.App.ViewModels;

/// <summary>A well-known host to ping in one click.</summary>
public sealed record QuickTarget(string Name, string Host)
{
    public static IReadOnlyList<QuickTarget> Defaults { get; } =
    [
        new("Google DNS", "8.8.8.8"),
        new("Cloudflare DNS", "1.1.1.1"),
        new("Quad9 DNS", "9.9.9.9"),
    ];
}
