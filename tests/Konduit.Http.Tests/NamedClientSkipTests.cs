using Konduit;
using Microsoft.Extensions.DependencyInjection;

namespace Konduit.Http.Tests;

/// <summary>
/// A named typed client whose implementation carries <see cref="SkipKonduitAttribute"/>: the
/// intersection of naming a client and annotating the implementation rather than the interface.
/// </summary>
public sealed class NamedClientSkipTests
{
    [Fact]
    public async Task ANamedClientHonoursAnImplementationSkip()
    {
        var services = new ServiceCollection().AddSingleton<Recorder>();

        services.AddHttpClient<IInvoiceApi, InvoiceApi>("invoices", c => c.BaseAddress = new Uri("https://example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => new EchoHandler())
            .AddMiddleware<TracingMiddleware>();

        var provider = services.BuildServiceProvider();
        var api = provider.GetRequiredService<IInvoiceApi>();
        var recorder = provider.GetRequiredService<Recorder>();

        Assert.Equal("/invoices/7", await api.GetAsync(7, CancellationToken.None));
        Assert.Equal(
            ["InvoiceApi.GetAsync:before", "InvoiceApi.GetAsync:after=/invoices/7"],
            recorder.Calls);

        recorder.Calls.Clear();

        // Still reaches the backend, but runs no middleware.
        Assert.Equal("/ping", await api.PingAsync(CancellationToken.None));
        Assert.Empty(recorder.Calls);
    }

    [Fact]
    public async Task TheExplicitOverloadHonoursAnImplementationSkipToo()
    {
        var services = new ServiceCollection().AddSingleton<Recorder>();

        services.AddHttpClient<IInvoiceApi, InvoiceApi>("invoices-explicit", c => c.BaseAddress = new Uri("https://example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => new EchoHandler())
            .AddMiddleware<IInvoiceApi, TracingMiddleware>();

        var provider = services.BuildServiceProvider();

        Assert.Equal("/ping", await provider.GetRequiredService<IInvoiceApi>().PingAsync(CancellationToken.None));
        Assert.Empty(provider.GetRequiredService<Recorder>().Calls);
    }

    [Fact]
    public async Task ANamedClientReachedThroughAConsumingServiceIsStillWrapped()
    {
        var services = new ServiceCollection().AddSingleton<Recorder>();

        // The named client is configured on its own, as a shared library might do...
        services.AddHttpClient("invoices-shared", c => c.BaseAddress = new Uri("https://example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => new EchoHandler());

        // ...and the service in front of it gets the pipeline.
        services.AddScoped<IInvoiceApi>(sp =>
                new InvoiceApi(sp.GetRequiredService<IHttpClientFactory>().CreateClient("invoices-shared")))
            .WithMiddleware<TracingMiddleware>();

        var provider = services.BuildServiceProvider();

        Assert.Equal("/invoices/7", await provider.GetRequiredService<IInvoiceApi>().GetAsync(7, CancellationToken.None));
        Assert.Equal(
            ["InvoiceApi.GetAsync:before", "InvoiceApi.GetAsync:after=/invoices/7"],
            provider.GetRequiredService<Recorder>().Calls);
    }

    [Fact]
    public async Task AFactoryRegistrationThatNamesItsImplementationHonoursTheSkip()
    {
        var services = new ServiceCollection().AddSingleton<Recorder>();

        services.AddHttpClient("invoices-named-impl", c => c.BaseAddress = new Uri("https://example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => new EchoHandler());

        // AddScoped<TService, TImplementation>(factory) names the implementation in its second type
        // argument, so the generator can read [SkipKonduit] from it even though a lambda builds it.
        services.AddScoped<IInvoiceApi, InvoiceApi>(sp =>
                new InvoiceApi(sp.GetRequiredService<IHttpClientFactory>().CreateClient("invoices-named-impl")))
            .WithMiddleware<TracingMiddleware>();

        var provider = services.BuildServiceProvider();
        var api = provider.GetRequiredService<IInvoiceApi>();
        var recorder = provider.GetRequiredService<Recorder>();

        Assert.Equal("/invoices/7", await api.GetAsync(7, CancellationToken.None));
        Assert.Equal(2, recorder.Calls.Count);

        recorder.Calls.Clear();

        Assert.Equal("/ping", await api.PingAsync(CancellationToken.None));
        Assert.Empty(recorder.Calls);
    }
}
