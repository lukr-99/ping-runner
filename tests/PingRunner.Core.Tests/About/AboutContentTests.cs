using PingRunner.Core.About;

namespace PingRunner.Core.Tests.About;

public sealed class AboutContentTests
{
    private static readonly AboutContent Content = AboutContent.Current;

    [Fact]
    public void Author_IsLukasKrejciWithGitHubAndInstagram()
    {
        Assert.Equal("Lukáš Krejčí", Content.AuthorName);
        Assert.Contains(Content.AuthorLinks, link => link is { Label: "GitHub", Url: "https://github.com/lukr-99" });
        Assert.Contains(Content.AuthorLinks, link => link is { Label: "Instagram", Url: "https://www.instagram.com/lukas9krejci/" });
    }

    [Fact]
    public void Feedback_OffersTheBugTrackerAndTheReferenceAppsChannels()
    {
        Assert.Contains(Content.FeedbackLinks, link => link.Url == "https://github.com/lukr-99/ping-runner/issues/new");
        Assert.Contains(Content.FeedbackLinks, link => link.Label == "Instagram");
    }

    [Fact]
    public void EveryLink_IsAnAbsoluteHttpsAddressWithALabelAndAHint()
    {
        var links = Content.AuthorLinks.Concat(Content.FeedbackLinks).Append(Content.LicenceLink)
            .Concat(Content.Credits.Select(credit => new AboutLink(credit.Name, credit.Url, credit.Role)));
        foreach (var link in links)
        {
            Assert.True(Uri.TryCreate(link.Url, UriKind.Absolute, out var uri), link.Url);
            Assert.Equal(Uri.UriSchemeHttps, uri.Scheme);
            Assert.False(string.IsNullOrWhiteSpace(link.Label));
            Assert.False(string.IsNullOrWhiteSpace(link.Hint));
        }
    }

    [Fact]
    public void Text_HasNoEmDashesAndNothingIsEmpty()
    {
        var text = new[] { Content.Name, Content.Tagline, Content.Description, Content.AuthorNote, Content.FeedbackNote, Content.LicenceNote }
            .Concat(Content.DataLines).Concat(Content.OnlineLines).Concat(Content.Credits.Select(credit => credit.Role));
        foreach (var line in text)
        {
            Assert.False(string.IsNullOrWhiteSpace(line));
            Assert.DoesNotContain('—', line);
        }
    }

    [Fact]
    public void Credits_NameEachLibraryOnceWithALicence()
    {
        Assert.Equal(Content.Credits.Count, Content.Credits.Select(credit => credit.Name).Distinct().Count());
        Assert.All(Content.Credits, credit => Assert.False(string.IsNullOrWhiteSpace(credit.Licence)));
    }

    [Theory]
    [InlineData("2.2.0", true, "Release 2.2.0")]
    [InlineData("2.2.0-dev", false, "Development build 2.2.0-dev")]
    public void BuildLabel_SaysWhatKindOfBuildThisIs(string version, bool release, string expected) =>
        Assert.Equal(expected, AboutContent.BuildLabel(version, release));
}
