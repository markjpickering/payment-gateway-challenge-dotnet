using System.Net;
using System.Text;

namespace PaymentGateway.Api.Tests.TestSupport;

/// <summary>
/// A hand-written test double for <see cref="HttpMessageHandler"/>. A mocking framework is deliberately
/// not used here (the ADR-6 exception): SendAsync is protected, so substituting it needs elaborate setup
/// that is far less readable than this small stub. It records the last request it saw.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<Task<HttpResponseMessage>> _responder;

    private StubHttpMessageHandler(Func<Task<HttpResponseMessage>> responder) => _responder = responder;

    public int CallCount { get; private set; }
    public HttpMethod? LastMethod { get; private set; }
    public Uri? LastUri { get; private set; }
    public string? LastBody { get; private set; }

    /// <summary>Runs when a request arrives, before the response is returned (for example to move a fake clock).</summary>
    public Action? OnSend { get; set; }

    public static StubHttpMessageHandler Responding(HttpStatusCode statusCode, string json = "") =>
        new(() => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        }));

    public static StubHttpMessageHandler Throwing(Exception exception) =>
        new(() => Task.FromException<HttpResponseMessage>(exception));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        OnSend?.Invoke();
        LastMethod = request.Method;
        LastUri = request.RequestUri;
        LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        return await _responder();
    }
}