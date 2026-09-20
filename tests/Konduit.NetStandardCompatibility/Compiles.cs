using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Konduit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Konduit.NetStandardCompatibility;

public interface IGreeter
{
    string Greet(string name);

    Task<string> GreetAsync(string name, CancellationToken cancellationToken);

    void Record(string name);

    [SkipKonduit]
    string GreetDirectly(string name);

    string this[int index] { get; }
}

public sealed class Greeter : IGreeter
{
    public string Greet(string name) => name;

    public Task<string> GreetAsync(string name, CancellationToken cancellationToken) => Task.FromResult(name);

    public void Record(string name) { }

    public string GreetDirectly(string name) => name;

    public string this[int index] => index.ToString();
}

public interface IOrderApi
{
    Task<string> GetAsync(int id, CancellationToken cancellationToken);
}

public sealed class OrderApi : IOrderApi
{
    private readonly HttpClient _http;

    public OrderApi(HttpClient http) => _http = http;

    public Task<string> GetAsync(int id, CancellationToken cancellationToken) =>
        _http.GetStringAsync($"/orders/{id}");
}

public sealed class Describe : IRequest<string>
{
    public int Id { get; set; }
}

public sealed class DescribeHandler : IRequestHandler<Describe, string>
{
    public Task<string> Handle(Describe request, CancellationToken cancellationToken) =>
        Task.FromResult(request.Id.ToString());
}

public sealed class LoggingMiddleware : IKonduitMiddleware
{
    private readonly KonduitDelegate _next;

    public LoggingMiddleware(KonduitDelegate next) => _next = next;

    public async ValueTask InvokeAsync(KonduitContext context)
    {
        await _next(context);
        _ = context.Method.Name;
        _ = context.Result;
    }
}

public static class Registration
{
    public static void Configure(IServiceCollection services)
    {
        // Core: a generated proxy, with its module initializer and attribute polyfills.
        services.AddScoped<IGreeter, Greeter>().WithMiddleware<LoggingMiddleware>();

        // Konduit.Http: a typed client wrapped through IHttpClientBuilder.
        services.AddHttpClient<IOrderApi, OrderApi>().AddMiddleware<LoggingMiddleware>();

        // Konduit.MediatR: handlers wrapped by hand-written decorators.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Registration).Assembly));
        services.AddKonduitToMediatRHandlers().WithMiddleware<LoggingMiddleware>();
    }
}
