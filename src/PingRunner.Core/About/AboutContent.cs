namespace PingRunner.Core.About;

/// <summary>
/// Everything the About page says that does not depend on the build: the description, who made it, where
/// to send feedback, what stays on the computer, what goes online and what Ping Runner is built on. The
/// page shows this and nothing else, so the words and links live in one place.
/// </summary>
public sealed record AboutContent(
    string Name,
    string Tagline,
    string Description,
    string AuthorName,
    string AuthorNote,
    IReadOnlyList<AboutLink> AuthorLinks,
    string FeedbackNote,
    IReadOnlyList<AboutLink> FeedbackLinks,
    IReadOnlyList<string> DataLines,
    IReadOnlyList<string> OnlineLines,
    string LicenceName,
    string LicenceNote,
    AboutLink LicenceLink,
    IReadOnlyList<AboutCredit> Credits)
{
    public const string RepositoryUrl = "https://github.com/lukr-99/ping-runner";

    /// <summary>Ping Runner's own About text.</summary>
    public static AboutContent Current { get; } = new(
        Name: "Ping Runner",
        Tagline: "Find out what is wrong with your connection.",
        Description: "Ping Runner pings a host for as long as you like and shows latency, jitter, packet loss and "
            + "outages as they happen. It also runs a speed test that measures how much a full line slows "
            + "everything else down, and makes reports you can hand to a support desk.",
        AuthorName: "Lukáš Krejčí",
        AuthorNote: "Ping Runner is designed and built by Lukáš Krejčí, as a personal project.",
        AuthorLinks:
        [
            new("GitHub", "https://github.com/lukr-99", "lukr-99"),
            new("Instagram", "https://www.instagram.com/lukas9krejci/", "lukas9krejci"),
        ],
        FeedbackNote: "Feedback is welcome: what works, what does not, what is missing. A bug report is most useful "
            + "with the version above and a copy of the Monitor summary.",
        FeedbackLinks:
        [
            new("Report a bug", RepositoryUrl + "/issues/new", "Opens a new issue on GitHub"),
            new("Ideas and questions", RepositoryUrl + "/issues", "Browse and add to the open issues"),
            new("Instagram", "https://www.instagram.com/lukas9krejci/", "Send a message"),
        ],
        DataLines:
        [
            "Everything stays on this computer. Settings and the history of ping runs and speed tests are two "
                + "files in the settings folder. There is no account and no tracking.",
            "A report or a backup is a file you choose to save. Ping Runner never sends one anywhere.",
        ],
        OnlineLines:
        [
            "Pings go to the host you enter, and only while you run them.",
            "A speed test uses Cloudflare's public speed-test service and pings 1.1.1.1.",
            "The Connection page asks api.ipify.org (then checkip.amazonaws.com) for your public IP address when it opens.",
            "\"Check for updates\" asks GitHub for the newest release, only when you click it, and never downloads anything.",
        ],
        LicenceName: "PolyForm Noncommercial 1.0.0",
        LicenceNote: "Free for personal and other noncommercial use. Selling it or using it commercially needs permission.",
        LicenceLink: new("Read the licence", RepositoryUrl + "/blob/main/LICENSE.md", "LICENSE.md on GitHub"),
        Credits:
        [
            new("WPF UI", "Windows 11 look and navigation", "MIT", "https://github.com/lepoco/wpfui"),
            new("CommunityToolkit.Mvvm", "View models", "MIT", "https://github.com/CommunityToolkit/dotnet"),
            new("SQLite", "The history database", "Public domain", "https://sqlite.org"),
            new("PDFsharp and MigraDoc", "PDF reports", "MIT", "https://github.com/empira/PDFsharp"),
            new("ClosedXML", "Excel reports and import", "MIT", "https://github.com/ClosedXML/ClosedXML"),
            new("Segoe UI", "Text in reports, from your Windows fonts folder", "Windows terms", "https://learn.microsoft.com/windows/apps/design/downloads/#fonts"),
        ]);

    /// <summary>"Release 2.2.0" or "Development build 2.2.0-dev".</summary>
    public static string BuildLabel(string version, bool isReleaseBuild) =>
        isReleaseBuild ? $"Release {version}" : $"Development build {version}";
}
