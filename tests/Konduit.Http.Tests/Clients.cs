using System.Net;
using Konduit;

namespace Konduit.Http.Tests;

public interface IOrderApi
{
    Task<string> GetAsync(int id, CancellationToken cancellationToken);

    [SkipKonduit]
    Task<string> PingAsync(CancellationToken cancellationToken);
}

public sealed class OrderApi(HttpClient http) : IOrderApi
{
    public Task<string> GetAsync(int id, CancellationToken cancellationToken) =>
        http.GetStringAsync($"/orders/{id}", cancellationToken);

    public Task<string> PingAsync(CancellationToken cancellationToken) =>
        http.GetStringAsync("/ping", cancellationToken);
}

public interface IBillingApi
{
    Task<string> GetAsync(int id, CancellationToken cancellationToken);
}

public sealed class BillingApi(HttpClient http) : IBillingApi
{
    public Task<string> GetAsync(int id, CancellationToken cancellationToken) =>
        http.GetStringAsync($"/bills/{id}", cancellationToken);
}

/// <summary>Answers every request with the path it was asked for, so no network is involved.</summary>
public sealed class EchoHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(request.RequestUri!.AbsolutePath),
        });
}

public sealed class Recorder
{
    public List<string> Calls { get; } = [];
}

public sealed class TracingMiddleware(KonduitDelegate next, Recorder recorder) : IKonduitMiddleware
{
    public async ValueTask InvokeAsync(KonduitContext context)
    {
        recorder.Calls.Add($"{context.Target.GetType().Name}.{context.Method.Name}:before");
        await next(context);
        recorder.Calls.Add($"{context.Target.GetType().Name}.{context.Method.Name}:after={context.Result}");
    }
}

public sealed class OuterMiddleware(KonduitDelegate next, Recorder recorder) : IKonduitMiddleware
{
    public async ValueTask InvokeAsync(KonduitContext context)
    {
        recorder.Calls.Add("outer:before");
        await next(context);
        recorder.Calls.Add("outer:after");
    }
}

public sealed class InnerMiddleware(KonduitDelegate next, Recorder recorder) : IKonduitMiddleware
{
    public async ValueTask InvokeAsync(KonduitContext context)
    {
        recorder.Calls.Add("inner:before");
        await next(context);
        recorder.Calls.Add("inner:after");
    }
}
