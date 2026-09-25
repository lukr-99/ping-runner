using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using PingRunner.Core.Updates;

namespace PingRunner.Infrastructure.Updates;

/// <summary>
/// The newest release of a public GitHub repository, from the REST API's <c>releases/latest</c>,
/// which skips drafts and pre-releases. A 404 means nothing is published yet. The response is untrusted:
/// the tag must be a version and the page must be on github.com over HTTPS.
/// </summary>
public sealed class GitHubReleaseSource(HttpClient http, string owner, string repository, string userAgent) : IReleaseSource
{
    public async Task<ReleaseInfo?> GetLatestAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri($"https://api.github.com/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}/releases/latest"));
        request.Headers.UserAgent.Add(ProductInfoHeaderValue.Parse(userAgent));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            return Parse(document.RootElement);
        }
    }

    private static ReleaseInfo Parse(JsonElement release)
    {
        var tag = release.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
        var version = ReleaseVersion.TryParse(tag)
            ?? throw new InvalidDataException($"The latest release tag '{tag}' is not a version.");

        var page = release.TryGetProperty("html_url", out var pageElement) ? pageElement.GetString() : null;
        if (!Uri.TryCreate(page, UriKind.Absolute, out var pageUrl)
            || pageUrl.Scheme != Uri.UriSchemeHttps
            || !string.Equals(pageUrl.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The latest release has no github.com page.");
        }

        var title = release.TryGetProperty("name", out var nameElement) && nameElement.GetString() is { Length: > 0 } name
            ? name
            : $"Ping Runner {version}";
        DateTimeOffset? publishedAt = release.TryGetProperty("published_at", out var publishedElement)
            && publishedElement.ValueKind == JsonValueKind.String
            && publishedElement.TryGetDateTimeOffset(out var published)
                ? published
                : null;

        return new ReleaseInfo(version, title, pageUrl, publishedAt);
    }
}
