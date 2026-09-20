using Konduit;
using Microsoft.Extensions.DependencyInjection;

namespace Konduit.Http.Tests;

/// <summary>
/// "Named client" covers two different registrations: a typed client given a name, which Konduit
/// can wrap, and a bare named client with no service interface, which it cannot.
/// </summary>
public sealed class NamedClientTests
{
    [Fact]
    public async Task ATypedClientWithACustomNameIsWrappedWithoutNamingTheService()
    {
        var services = new ServiceCollection().AddSingleton<Recorder>();

        // builder.Name is "orders", so inference cannot match on the name and falls back to the
        // most recent wrappable typed client — which is this one.
        services.AddHttpClient<IOrderApi, OrderApi>("orders", c => c.BaseAddress = new Uri("https://example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => new EchoHandler())
            .AddMiddleware<TracingMiddleware>();

        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IOrderApi>().GetAsync(7, CancellationToken.None);

        Assert.Equal(
            ["OrderApi.GetAsync:before", "OrderApi.GetAsync:after=/orders/7"],
            provider.GetRequiredService<Recorder>().Calls);
    }

    [Fact]
    public async Task SeveralTypedClientsWithCustomNamesEachGetTheirOwnPipeline()
    {
        var services = new ServiceCollection().AddSingleton<Recorder>();

        services.AddHttpClient<IOrderApi, OrderApi>("orders", c => c.BaseAddress = new Uri("https://example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => new EchoHandler())
            .AddMiddleware<TracingMiddleware>();

        services.AddHttpClient<IBillingApi, BillingApi>("bills", c => c.BaseAddress = new Uri("https://example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => new EchoHandler())
            .AddMiddleware<TracingMiddleware>();

        var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IOrderApi>().GetAsync(7, CancellationToken.None);
        await provider.GetRequiredService<IBillingApi>().GetAsync(9, CancellationToken.None);

        Assert.Equal(
            [
                "OrderApi.GetAsync:before",
                "OrderApi.GetAsync:after=/orders/7",
                "BillingApi.GetAsync:before",
                "BillingApi.GetAsync:after=/bills/9",
            ],
            provider.GetRequiredService<Recorder>().Calls);
    }

    [Fact]
    public void ABareNamedClientHasNoServiceToWrap()
    {
        var services = new ServiceCollection();

        // AddHttpClient("orders") registers no service interface at all: the client is reached
        // through IHttpClientFactory.CreateClient("orders"), so there are no method calls to intercept.
        var builder = services.AddHttpClient("orders");

        var ex = Assert.Throws<InvalidOperationException>(
            () => builder.AddMiddleware<IOrderApi, TracingMiddleware>());

        Assert.Contains("AddHttpClient<TClient, TImplementation>()", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ABareNamedClientUsedByAWrappedServiceStillGetsMiddleware()
    {
        var services = new ServiceCollection().AddSingleton<Recorder>();

        // The supported shape: keep the named client for its HttpClient configuration, and put the
        // middleware on the service that consumes it.
        services.AddHttpClient("orders", c => c.BaseAddress = new Uri("https://example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => new EchoHandler());

        services.AddScoped<IOrderApi>(sp =>
                new OrderApi(sp.GetRequiredService<IHttpClientFactory>().CreateClient("orders")))
            .WithMiddleware<TracingMiddleware>();

        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IOrderApi>().GetAsync(7, CancellationToken.None);

        Assert.Equal(
            ["OrderApi.GetAsync:before", "OrderApi.GetAsync:after=/orders/7"],
            provider.GetRequiredService<Recorder>().Calls);
    }
}
