using System.Net;
using System.Text;
using System.Text.Json;

namespace OSSDependencyAnalyzer.Tests.TestSupport;

/// <summary>Routes requests to canned responses by "METHOD path-and-query" prefix and records every call.</summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly List<(Func<HttpRequestMessage, bool> Match, Func<HttpRequestMessage, Task<HttpResponseMessage>> Respond)> _routes = [];

    public List<(HttpMethod Method, string Url, string? Body)> Requests { get; } = [];

    public StubHttpMessageHandler On(Func<HttpRequestMessage, bool> match, Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _routes.Add((match, r => Task.FromResult(respond(r))));
        return this;
    }

    public StubHttpMessageHandler OnGet(string pathPrefix, HttpStatusCode status, string? body = null) =>
        On(r => r.Method == HttpMethod.Get && r.RequestUri!.PathAndQuery.StartsWith(pathPrefix, StringComparison.Ordinal),
            _ => Text(status, body));

    public StubHttpMessageHandler OnGetJson(string pathPrefix, object payload) =>
        OnGet(pathPrefix, HttpStatusCode.OK, JsonSerializer.Serialize(payload));

    public StubHttpMessageHandler OnPostJson(string pathPrefix, Func<string, object> payloadFromBody) =>
        On(r => r.Method == HttpMethod.Post && r.RequestUri!.PathAndQuery.StartsWith(pathPrefix, StringComparison.Ordinal),
            r => Text(HttpStatusCode.OK, JsonSerializer.Serialize(payloadFromBody(r.Content!.ReadAsStringAsync().GetAwaiter().GetResult()))));

    public static HttpResponseMessage Text(HttpStatusCode status, string? body) =>
        new(status) { Content = new StringContent(body ?? string.Empty, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.Method, request.RequestUri!.ToString(), body));

        foreach (var (match, respond) in _routes)
        {
            if (match(request))
                return await respond(request);
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request };
    }
}

public sealed class StubHttpClientFactory(Dictionary<string, (HttpMessageHandler Handler, string BaseAddress)> clients) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) =>
        clients.TryGetValue(name, out var client)
            ? new HttpClient(client.Handler, disposeHandler: false) { BaseAddress = new Uri(client.BaseAddress) }
            : throw new InvalidOperationException($"No stub registered for HttpClient '{name}'.");
}
