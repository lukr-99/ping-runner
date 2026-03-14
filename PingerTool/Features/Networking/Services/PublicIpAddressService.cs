using System.Net.Http;

namespace PingerTool.Features.Networking.Services;

public sealed class PublicIpAddressService
{
    private static readonly Uri[] EndpointUris =
    [
        new("https://api.ipify.org"),
        new("https://checkip.amazonaws.com"),
    ];

    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5),
    };

    public async Task<string?> TryGetCurrentIpAddressAsync(CancellationToken cancellationToken = default)
    {
        foreach (var endpointUri in EndpointUris)
        {
            try
            {
                using var response = await _httpClient.GetAsync(endpointUri, cancellationToken);
                response.EnsureSuccessStatusCode();

                var ipAddress = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();

                if (!string.IsNullOrWhiteSpace(ipAddress))
                {
                    return ipAddress;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }
            catch (HttpRequestException)
            {
            }
        }

        return null;
    }
}
