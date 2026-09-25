using System.Net;

namespace PingRunner.Infrastructure.Tests.Fakes;

/// <summary>Answers every request with the response the test chose, and remembers the requests.</summary>
public sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public List<long> UploadedBodyLengths { get; } = [];

    public static StubHttpHandler Text(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(_ => new HttpResponseMessage(status) { Content = new StringContent(body) });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (request.Content is not null)
        {
            var body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            UploadedBodyLengths.Add(body.LongLength);
        }

        return respond(request);
    }
}
