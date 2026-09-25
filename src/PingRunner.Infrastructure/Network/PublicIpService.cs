using System.Net;
using PingRunner.Core.Network;

namespace PingRunner.Infrastructure.Network;

/// <summary>
/// Asks ipify first and Amazon's checkip second which address the internet sees. Answers are
/// untrusted: only a well-formed IP address is accepted.
/// </summary>
public sealed class PublicIpService(HttpClient http) : IPublicIpSource
{
    private static readonly Uri[] Endpoints =
    [
        new("https://api.ipify.org"),
        new("https://checkip.amazonaws.com"),
    ];

    public async Task<string?> GetAsync(CancellationToken cancellationToken)
    {
        foreach (var endpoint in Endpoints)
        {
            try
            {
                using var response = await http.GetAsync(endpoint, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var text = (await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)).Trim();
                if (text.Length <= 45 && IPAddress.TryParse(text, out var address))
                {
                    return address.ToString();
                }
            }
            catch (HttpRequestException)
            {
                // Try the next service.
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timed out; try the next service.
            }
        }

        return null;
    }
}
