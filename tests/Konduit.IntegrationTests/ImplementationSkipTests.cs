using Konduit;
using Microsoft.Extensions.DependencyInjection;

namespace Konduit.IntegrationTests;

/// <summary>
/// <see cref="SkipKonduitAttribute"/> on the implementation rather than the interface, which is the
/// only option when the interface comes from a package you cannot edit.
/// </summary>
public sealed class ImplementationSkipTests
{
    [Fact]
    public void AnImplementationCanSkipAMethodTheInterfaceDoesNot()
    {
        var provider = new ServiceCollection()
            .AddSingleton<Trace>()
            .AddScoped<IThirdPartyApi, OurApi>()
            .WithMiddleware<TracingMiddleware>()
            .BuildServiceProvider();

        var api = provider.GetRequiredService<IThirdPartyApi>();
        var trace = provider.GetRequiredService<Trace>();

        Assert.Equal("item 7", api.Fetch(7));
        Assert.Equal(["Fetch:before", "Fetch:after=item 7"], trace.Entries);

        trace.Entries.Clear();

        Assert.Equal("pong", api.Ping());
        Assert.Empty(trace.Entries);
    }

    [Fact]
    public void AnotherImplementationOfTheSameInterfaceIsUnaffected()
    {
        var provider = new ServiceCollection()
            .AddSingleton<Trace>()
            .AddScoped<IThirdPartyApi, PlainApi>()
            .WithMiddleware<TracingMiddleware>()
            .BuildServiceProvider();

        var api = provider.GetRequiredService<IThirdPartyApi>();
        var trace = provider.GetRequiredService<Trace>();

        // PlainApi marks nothing, so both methods stay intercepted even though OurApi skips Ping.
        api.Fetch(7);
        api.Ping();

        Assert.Equal(
            ["Fetch:before", "Fetch:after=plain 7", "Ping:before", "Ping:after=plain pong"],
            trace.Entries);
    }

    [Fact]
    public void TheTypeofRegistrationFormIsUnderstoodToo()
    {
        var provider = new ServiceCollection()
            .AddSingleton<Trace>()
            .AddScoped(typeof(IThirdPartyApi), typeof(OurApi))
            .WithMiddleware<TracingMiddleware>()
            .BuildServiceProvider();

        var trace = provider.GetRequiredService<Trace>();

        Assert.Equal("pong", provider.GetRequiredService<IThirdPartyApi>().Ping());
        Assert.Empty(trace.Entries);
    }

    [Fact]
    public void AFactoryRegistrationStillHonoursAnImplementationTheGeneratorHasSeen()
    {
        // The registration itself names no implementation, but OurApi is named in a typed
        // registration elsewhere in this assembly, so its specialised proxy exists and the wrapped
        // instance's type selects it.
        var provider = new ServiceCollection()
            .AddSingleton<Trace>()
            .AddScoped<IThirdPartyApi>(_ => new OurApi())
            .WithMiddleware<TracingMiddleware>()
            .BuildServiceProvider();

        var trace = provider.GetRequiredService<Trace>();

        Assert.Equal("pong", provider.GetRequiredService<IThirdPartyApi>().Ping());
        Assert.Empty(trace.Entries);
    }

    [Fact]
    public void AnImplementationTheGeneratorNeverSeesKeepsItsMethodsIntercepted()
    {
        // UnseenApi is never named in a registration the generator can read, so no specialised proxy
        // exists for it and the shared one — which only knows the interface — is used instead.
        var provider = new ServiceCollection()
            .AddSingleton<Trace>()
            .AddScoped<IThirdPartyApi>(_ => new UnseenApi())
            .WithMiddleware<TracingMiddleware>()
            .BuildServiceProvider();

        var trace = provider.GetRequiredService<Trace>();

        Assert.Equal("unseen pong", provider.GetRequiredService<IThirdPartyApi>().Ping());
        Assert.Equal(["Ping:before", "Ping:after=unseen pong"], trace.Entries);
    }
}

/// <summary>Stands in for an interface from a package that cannot be annotated.</summary>
public interface IThirdPartyApi
{
    string Fetch(int id);

    string Ping();
}

public sealed class OurApi : IThirdPartyApi
{
    public string Fetch(int id) => $"item {id}";

    [SkipKonduit]
    public string Ping() => "pong";
}

public sealed class PlainApi : IThirdPartyApi
{
    public string Fetch(int id) => $"plain {id}";

    public string Ping() => "plain pong";
}

public sealed class UnseenApi : IThirdPartyApi
{
    public string Fetch(int id) => $"unseen {id}";

    [SkipKonduit]
    public string Ping() => "unseen pong";
}
