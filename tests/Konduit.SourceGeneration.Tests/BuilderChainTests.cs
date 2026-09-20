namespace Konduit.SourceGeneration.Tests;

/// <summary>
/// Chains through a builder, as Konduit.Http does with IHttpClientBuilder. A stand-in builder is
/// declared in the snippets so these stay independent of Microsoft.Extensions.Http.
/// </summary>
public sealed class BuilderChainTests
{
    private const string Preamble = """
        using System;
        using System.Threading.Tasks;
        using Konduit;
        using Microsoft.Extensions.DependencyInjection;

        public sealed class Noop : IKonduitMiddleware
        {
            public Noop(KonduitDelegate next) { }
            public ValueTask InvokeAsync(KonduitContext context) => default;
        }

        public interface IClientBuilder { }

        public static class FakeHttp
        {
            public static IClientBuilder AddClient<TClient, TImplementation>(this IServiceCollection services) => null!;
            public static IClientBuilder ConfigureClient(this IClientBuilder builder, Action<string> configure) => builder;
            public static IClientBuilder AddHandler<THandler>(this IClientBuilder builder) => builder;

            public static IClientBuilder AddMiddleware<TMiddleware>(this IClientBuilder builder)
                where TMiddleware : class, IKonduitMiddleware => builder;

            public static IClientBuilder AddMiddleware<TClient, TMiddleware>(this IClientBuilder builder)
                where TMiddleware : class, IKonduitMiddleware => builder;
        }

        public interface IGreeter { string Greet(string name); }
        public sealed class Greeter : IGreeter { public string Greet(string name) => name; }

        """;

    private static GeneratorResult Run(string body) => GeneratorHarness.Run(Preamble + body);

    [Fact]
    public void AddMiddlewareOnABuilderResolvesTheServiceFromTheChain()
    {
        var result = Run("""
            public static class Startup
            {
                public static void Configure(IServiceCollection services) =>
                    services.AddClient<IGreeter, Greeter>().AddMiddleware<Noop>();
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Contains("IGreeterKonduitProxy", result.AllGeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void IntermediateBuilderCallsAreSkipped()
    {
        var result = Run("""
            public static class Startup
            {
                public static void Configure(IServiceCollection services) =>
                    services.AddClient<IGreeter, Greeter>()
                            .ConfigureClient(_ => { })
                            .AddHandler<Greeter>()
                            .AddMiddleware<Noop>();
            }
            """);

        // AddHandler<Greeter> names a class, but it is not the registration: the walk keeps going
        // back until it reaches the interface the client was registered with.
        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Contains("IGreeterKonduitProxy", result.AllGeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void NamingTheServiceExplicitlyNeedsNoChain()
    {
        var result = Run("""
            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    var builder = services.AddClient<IGreeter, Greeter>();
                    builder.AddMiddleware<IGreeter, Noop>();
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Contains("IGreeterKonduitProxy", result.AllGeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void AHeldBuilderWithoutAnExplicitServiceIsReportedAsKdt001()
    {
        var result = Run("""
            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    var builder = services.AddClient<IGreeter, Greeter>();
                    builder.AddMiddleware<Noop>();
                }
            }
            """);

        Assert.Equal(["KDT001"], result.DiagnosticIds);
        Assert.Contains(
            "name the service explicitly",
            result.GeneratorDiagnostics[0].GetMessage(),
            StringComparison.Ordinal);
    }
}
