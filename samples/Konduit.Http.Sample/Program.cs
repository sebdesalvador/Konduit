using Konduit;
using Konduit.Http.Sample;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddHttpClient<IOrderApi, OrderApi>(client => client.BaseAddress = new Uri("https://api.example.test"))
    .ConfigurePrimaryHttpMessageHandler(() => new FakeBackend())
    .AddHttpMessageHandler<CorrelationHandler>()   // a DelegatingHandler, underneath
    .AddMiddleware<LoggingMiddleware>()            // Konduit middleware, around the service
    .AddMiddleware<RetryMiddleware>()
    .AddMiddleware<CachingMiddleware>();

services.AddTransient<CorrelationHandler>();

var provider = services.BuildServiceProvider();
var api = provider.GetRequiredService<IOrderApi>();

Console.WriteLine("A call that fails once — the retry re-runs the whole method:");
Console.WriteLine($"  result: {await api.GetAsync(7, CancellationToken.None)}");

Console.WriteLine();
Console.WriteLine("The same call again — served from the cache, no HTTP at all:");
Console.WriteLine($"  result: {await api.GetAsync(7, CancellationToken.None)}");

Console.WriteLine();
Console.WriteLine("[SkipKonduit] on the implementation — no middleware, HTTP still happens:");
Console.WriteLine($"  result: {await api.PingAsync(CancellationToken.None)}");

Console.WriteLine();
Console.WriteLine($"The resolved service is a generated proxy: {api.GetType().Name}");
Console.WriteLine($"It wraps the real typed client:            {((IKonduitProxy)api).KonduitTarget.GetType().Name}");
