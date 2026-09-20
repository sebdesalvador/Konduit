namespace Konduit.SourceGeneration.Tests;

/// <summary>
/// The generator reads <c>[SkipKonduit]</c> from the implementation named in the registration, so an
/// interface that cannot be annotated is still able to opt methods out.
/// </summary>
public sealed class ImplementationSkipTests
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

        // No Konduit attributes: stands in for an interface from a package you cannot edit.
        public interface IGreeter
        {
            string Greet(string name);
            string Ping();
        }

        """;

    private static GeneratorResult Run(string body) => GeneratorHarness.Run(Preamble + body);

    [Fact]
    public void AnImplementationSkipProducesASecondSpecialisedProxy()
    {
        var result = Run("""
            public sealed class Greeter : IGreeter
            {
                public string Greet(string name) => name;
                [SkipKonduit] public string Ping() => "pong";
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services) =>
                    services.AddScoped<IGreeter, Greeter>().WithMiddleware<Noop>();
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        // The shared proxy plus one specialised for Greeter, alongside the registrations file.
        Assert.Equal(3, result.GeneratedSources.Length);
        Assert.Contains("=> _target.Ping();", result.AllGeneratedSource, StringComparison.Ordinal);

        // The specialised proxy is registered against the implementation as well as the interface.
        Assert.Contains("typeof(global::Greeter),", result.AllGeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void AnImplementationThatSkipsNothingGetsNoExtraProxy()
    {
        var result = Run("""
            public sealed class Greeter : IGreeter
            {
                public string Greet(string name) => name;
                public string Ping() => "pong";
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services) =>
                    services.AddScoped<IGreeter, Greeter>().WithMiddleware<Noop>();
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);

        // One proxy plus the registrations file: a second would describe the same behaviour.
        Assert.Equal(2, result.GeneratedSources.Length);
    }

    [Fact]
    public void TwoImplementationsSkippingDifferentMethodsGetAProxyEach()
    {
        var result = Run("""
            public sealed class Chatty : IGreeter
            {
                public string Greet(string name) => name;
                [SkipKonduit] public string Ping() => "pong";
            }

            public sealed class Quiet : IGreeter
            {
                [SkipKonduit] public string Greet(string name) => name;
                public string Ping() => "pong";
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddScoped<IGreeter, Chatty>().WithMiddleware<Noop>();
                    services.AddScoped<IGreeter, Quiet>().WithMiddleware<Noop>();
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        // Shared, Chatty, Quiet, and the registrations file.
        Assert.Equal(4, result.GeneratedSources.Length);
    }

    [Fact]
    public void TheTypeofRegistrationFormNamesTheImplementationToo()
    {
        var result = Run("""
            public sealed class Greeter : IGreeter
            {
                public string Greet(string name) => name;
                [SkipKonduit] public string Ping() => "pong";
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services) =>
                    services.AddScoped(typeof(IGreeter), typeof(Greeter)).WithMiddleware<Noop>();
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Equal(3, result.GeneratedSources.Length);
    }

    [Fact]
    public void TheMiddlewareTypeIsNeverMistakenForAnImplementation()
    {
        // AddMiddleware<TClient, TMiddleware>() names the middleware second, not an implementation.
        var result = Run("""
            public interface IClientBuilder { }

            public static class FakeHttp
            {
                public static IClientBuilder AddClient<TClient, TImplementation>(this IServiceCollection services) => null!;

                public static IClientBuilder AddMiddleware<TClient, TMiddleware>(this IClientBuilder builder)
                    where TMiddleware : class, IKonduitMiddleware => builder;
            }

            public sealed class Greeter : IGreeter
            {
                public string Greet(string name) => name;
                public string Ping() => "pong";
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services) =>
                    services.AddClient<IGreeter, Greeter>().AddMiddleware<IGreeter, Noop>();
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
        Assert.Equal(2, result.GeneratedSources.Length);
    }
}
