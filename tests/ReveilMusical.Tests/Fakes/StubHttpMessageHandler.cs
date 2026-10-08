using System.Net;
using System.Text;

namespace ReveilMusical.Tests.Fakes;

/// <summary>Simule HTTP : aucune requête réseau réelle en test. Garde les requêtes reçues.</summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public List<Uri> RequestUris { get; } = [];

    public List<string> UserAgents { get; } = [];

    public int Calls => RequestUris.Count;

    public static StubHttpMessageHandler Json(string body, string mediaType = "application/json") =>
        new(_ => JsonResponse(body, mediaType));

    public static StubHttpMessageHandler Status(HttpStatusCode status) =>
        new(_ => new HttpResponseMessage(status));

    public static StubHttpMessageHandler Throwing(Exception exception) =>
        new(_ => throw exception);

    public static HttpResponseMessage JsonResponse(string body, string mediaType = "application/json") =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, mediaType) };

    public HttpClient CreateClient(string baseAddress = "https://stub.test/") =>
        new(this) { BaseAddress = new Uri(baseAddress) };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequestUris.Add(request.RequestUri!);
        UserAgents.Add(request.Headers.UserAgent.ToString());
        return Task.FromResult(_responder(request));
    }
}
