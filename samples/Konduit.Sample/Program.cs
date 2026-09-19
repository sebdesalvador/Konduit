using Konduit;
using Konduit.Sample;
using Microsoft.Extensions.DependencyInjection;

var provider = new ServiceCollection()
    .AddSingleton<IOrderService, OrderService>()
    .WithMiddleware<LoggingMiddleware>()
    .WithMiddleware<RetryMiddleware>()
    .WithMiddleware<CachingMiddleware>()
    .BuildServiceProvider();

var orders = provider.GetRequiredService<IOrderService>();

Console.WriteLine("An async call that fails once and is retried:");
Console.WriteLine($"  result: {await orders.PlaceAsync("a desk lamp", CancellationToken.None)}");

Console.WriteLine();
Console.WriteLine("A synchronous call, twice — the second is served from the cache:");
orders.Describe(7);
orders.Describe(7);

Console.WriteLine();
Console.WriteLine("A [SkipKonduit] method runs no middleware at all:");
Console.WriteLine($"  attempts made: {orders.Count()}");

Console.WriteLine();
Console.WriteLine($"The resolved service is a generated proxy: {orders.GetType().Name}");
Console.WriteLine($"The implementation stays reachable unwrapped: {((IKonduitProxy)orders).KonduitTarget.GetType().Name}");
