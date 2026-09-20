using Konduit;
using Microsoft.Extensions.DependencyInjection;

namespace Konduit.Http.Tests;

/// <summary>
/// Chains are written out in full in each test rather than shared through a helper: the source
/// generator reads the registration chain syntactically, so a helper returning an
/// <see cref="IHttpClientBuilder"/> would hide the service type from it.
/// </summary>
public sealed class KonduitHttpClientTests
{
    [Fact]
    public async Task TheTypedClientIsWrappedInThePipeline()
    {
        var services = new ServiceCollection().AddSingleton<Recorder>();
        services.AddHttpClient<IOrderApi, OrderApi>(c => c.BaseAddress = new Uri("https://example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => new EchoHandler())
            .AddMiddleware<TracingMiddleware>();

        var provider = services.BuildServiceProvider();
        var api = provider.GetRequiredService<IOrderApi>();

        Assert.IsAssignableFrom<IKonduitProxy>(api);
        Assert.Equal("/orders/7", await api.GetAsync(7, CancellationToken.None));
        Assert.Equal(
            ["OrderApi.GetAsync:before", "OrderApi.GetAsync:after=/orders/7"],
            provider.GetRequiredService<Recorder>().Calls);
    }

    [Fact]
    public void TheHttpClientIsStillInjectedIntoTheImplementation()
    {
        var services = new ServiceCollection().AddSingleton<Recorder>();
        services.AddHttpClient<IOrderApi, OrderApi>(c => c.BaseAddress = new Uri("https://example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => new EchoHandler())
            .AddMiddleware<TracingMiddleware>();

        var api = services.BuildServiceProvider().GetRequiredService<IOrderApi>();

        // The proxy wraps the real typed client, which the factory built with its HttpClient.
        Assert.IsType<OrderApi>(((IKonduitProxy)api).KonduitTarget);
    }

    [Fact]
    public async Task MiddlewareRunsOutsideInInRegistrationOrder()
    {
        var services = new ServiceCollection().AddSingleton<Recorder>();
        services.AddHttpClient<IOrderApi, OrderApi>(c => c.BaseAddress = new Uri("https://example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => new EchoHandler())
            .AddMiddleware<OuterMiddleware>()
            .AddMiddleware<InnerMiddleware>();

        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IOrderApi>().GetAsync(7, CancellationToken.None);

        Assert.Equal(
            ["outer:before", "inner:before", "inner:after", "outer:after"],
            provider.GetRequiredService<Recorder>().Calls);
    }

    [Fact]
    public async Task ChainedMiddlewareSharesOnePipelineRatherThanNestingProxies()
    {
        var services = new ServiceCollection().AddSingleton<Recorder>();
        services.AddHttpClient<IOrderApi, OrderApi>(c => c.BaseAddress = new Uri("https://example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => new EchoHandler())
            .AddMiddleware<OuterMiddleware>()
            .AddMiddleware<InnerMiddleware>();

        var provider = services.BuildServiceProvider();
        var api = provider.GetRequiredService<IOrderApi>();

        // One proxy around the client, not a proxy around a proxy.
        Assert.IsType<OrderApi>(((IKonduitProxy)api).KonduitTarget);

        await api.GetAsync(7, CancellationToken.None);
        Assert.Equal(4, provider.GetRequiredService<Recorder>().Calls.Count);
    }

    [Fact]
    public async Task SkipKonduitMethodsBypassThePipeline()
    {
        var services = new ServiceCollection().AddSingleton<Recorder>();
        services.AddHttpClient<IOrderApi, OrderApi>(c => c.BaseAddress = new Uri("https://example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => new EchoHandler())
            .AddMiddleware<TracingMiddleware>();

        var provider = services.BuildServiceProvider();

        Assert.Equal("/ping", await provider.GetRequiredService<IOrderApi>().PingAsync(CancellationToken.None));
        Assert.Empty(provider.GetRequiredService<Recorder>().Calls);
    }

    [Fact]
    public void MiddlewareCanFollowOtherBuilderCalls()
    {
        var services = new ServiceCollection().AddSingleton<Recorder>();

        // Each builder call adds descriptors of its own, so the typed client is no longer last.
        services.AddHttpClient<IOrderApi, OrderApi>()
            .ConfigurePrimaryHttpMessageHandler(() => new EchoHandler())
            .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(5))
            .AddMiddleware<TracingMiddleware>();

        Assert.IsAssignableFrom<IKonduitProxy>(services.BuildServiceProvider().GetRequiredService<IOrderApi>());
    }

    [Fact]
    public async Task EachClientGetsItsOwnPipelineWhenSeveralAreConfigured()
    {
        var services = new ServiceCollection().AddSingleton<Recorder>();

        services.AddHttpClient<IOrderApi, OrderApi>(c => c.BaseAddress = new Uri("https://example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => new EchoHandler())
            .AddMiddleware<TracingMiddleware>();

        services.AddHttpClient<IBillingApi, BillingApi>(c => c.BaseAddress = new Uri("https://example.test"))
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
    public async Task AHeldBuilderWrapsItsOwnClientWhenTheServiceIsNamed()
    {
        var services = new ServiceCollection().AddSingleton<Recorder>();

        // The builder is kept and used after a second client has been registered and wrapped, so
        // "the most recently wrapped candidate" would pick the wrong one.
        var orderBuilder = services.AddHttpClient<IOrderApi, OrderApi>(c => c.BaseAddress = new Uri("https://example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => new EchoHandler());

        services.AddHttpClient<IBillingApi, BillingApi>(c => c.BaseAddress = new Uri("https://example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => new EchoHandler())
            .AddMiddleware<TracingMiddleware>();

        orderBuilder.AddMiddleware<IOrderApi, TracingMiddleware>();

        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IOrderApi>().GetAsync(7, CancellationToken.None);

        Assert.Equal(
            ["OrderApi.GetAsync:before", "OrderApi.GetAsync:after=/orders/7"],
            provider.GetRequiredService<Recorder>().Calls);
    }

    [Fact]
    public async Task TheExplicitOverloadWorksForAClientWithACustomName()
    {
        var services = new ServiceCollection().AddSingleton<Recorder>();

        // builder.Name is "orders", not "IOrderApi", so inference by name cannot help here.
        services.AddHttpClient<IOrderApi, OrderApi>("orders", c => c.BaseAddress = new Uri("https://example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => new EchoHandler())
            .AddMiddleware<IOrderApi, TracingMiddleware>();

        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IOrderApi>().GetAsync(7, CancellationToken.None);

        Assert.Equal(
            ["OrderApi.GetAsync:before", "OrderApi.GetAsync:after=/orders/7"],
            provider.GetRequiredService<Recorder>().Calls);
    }

    [Fact]
    public void TypedClientsKeepTheirTransientLifetime()
    {
        var services = new ServiceCollection().AddSingleton<Recorder>();
        services.AddHttpClient<IOrderApi, OrderApi>(c => c.BaseAddress = new Uri("https://example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => new EchoHandler())
            .AddMiddleware<TracingMiddleware>();

        Assert.Equal(
            ServiceLifetime.Transient,
            services.Last(d => d.ServiceType == typeof(IOrderApi)).Lifetime);

        var provider = services.BuildServiceProvider();
        Assert.NotSame(provider.GetRequiredService<IOrderApi>(), provider.GetRequiredService<IOrderApi>());
    }

    [Fact]
    public void NamingAClientThatWasNeverRegisteredExplainsWhatIsMissing()
    {
        var services = new ServiceCollection();
        var builder = services.AddHttpClient("bare");

        var ex = Assert.Throws<InvalidOperationException>(
            () => builder.AddMiddleware<IOrderApi, TracingMiddleware>());

        Assert.Contains("AddHttpClient<TClient, TImplementation>()", ex.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(IOrderApi), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddMiddlewareOnANullBuilderThrows() =>
        Assert.Throws<ArgumentNullException>(
            () => KonduitHttpClientBuilderExtensions.AddMiddleware<IOrderApi, TracingMiddleware>(null!));
}
