using System.Net;
using Konduit;

namespace Konduit.Http.Sample;

/// <summary>
/// Stands in for a contract you do not own — generated from OpenAPI, or from a shared package — so
/// it carries no Konduit attributes.
/// </summary>
public interface IOrderApi
{
    Task<string> GetAsync(int id, CancellationToken cancellationToken);

    Task<string> PingAsync(CancellationToken cancellationToken);
}

public sealed class OrderApi(HttpClient http) : IOrderApi
{
    public Task<string> GetAsync(int id, CancellationToken cancellationToken) =>
        http.GetStringAsync($"/orders/{id}", cancellationToken);

    // The interface cannot be annotated, so the attribute goes on the implementation. A health
    // check does not need logging, retries or caching around it.
    [SkipKonduit]
    public Task<string> PingAsync(CancellationToken cancellationToken) =>
        http.GetStringAsync("/ping", cancellationToken);
}

/// <summary>Answers without a network, failing the first order request to show the retry.</summary>
public sealed class FakeBackend : HttpMessageHandler
{
    private int _attempts;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        Console.WriteLine($"        HTTP GET {path}");

        if (path.StartsWith("/orders", StringComparison.Ordinal) && ++_attempts == 1)
        {
            throw new HttpRequestException("the warehouse did not answer");
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($"payload for {path}"),
        });
    }
}

/// <summary>A DelegatingHandler, to show the two layers composing.</summary>
public sealed class CorrelationHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.Add("X-Correlation-Id", Guid.NewGuid().ToString("N")[..8]);
        Console.WriteLine("      handler: added X-Correlation-Id");
        return base.SendAsync(request, cancellationToken);
    }
}
