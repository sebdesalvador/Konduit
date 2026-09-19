namespace Konduit.SourceGeneration.Tests;

public sealed class KonduitProxyGeneratorTests
{
    private const string Preamble = """
        using System;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using Konduit;
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection.Extensions;

        public sealed class Noop : IKonduitMiddleware
        {
            public Noop(KonduitDelegate next) { }
            public ValueTask InvokeAsync(KonduitContext context) => default;
        }

        """;

    private static GeneratorResult Run(string body) => GeneratorHarness.Run(Preamble + body);

    [Fact]
    public void AChainedRegistrationGeneratesAProxyThatCompiles()
    {
        var result = Run("""
            public interface IGreeter { string Greet(string name); }
            public sealed class Greeter : IGreeter { public string Greet(string name) => name; }

            public static class Startup
            {
                public static void Configure(IServiceCollection services) =>
                    services.AddScoped<IGreeter, Greeter>().WithMiddleware<Noop>();
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
        Assert.Contains("IGreeterKonduitProxy", result.AllGeneratedSource, StringComparison.Ordinal);
        Assert.Contains("ModuleInitializer", result.AllGeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TheNonGenericRegistrationOverloadIsUnderstood()
    {
        var result = Run("""
            public interface IGreeter { string Greet(string name); }
            public sealed class Greeter : IGreeter { public string Greet(string name) => name; }

            public static class Startup
            {
                public static void Configure(IServiceCollection services) =>
                    services.AddScoped(typeof(IGreeter), typeof(Greeter)).WithMiddleware<Noop>();
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Contains("IGreeterKonduitProxy", result.AllGeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnchainedCallIsReportedAsCdt001()
    {
        var result = Run("""
            public interface IGreeter { string Greet(string name); }
            public sealed class Greeter : IGreeter { public string Greet(string name) => name; }

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddScoped<IGreeter, Greeter>();
                    services.WithMiddleware<Noop>();
                }
            }
            """);

        Assert.Equal(["KDT001"], result.DiagnosticIds);
        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void ANonInterfaceServiceIsReportedAsCdt002()
    {
        var result = Run("""
            public sealed class Greeter { public string Greet(string name) => name; }

            public static class Startup
            {
                public static void Configure(IServiceCollection services) =>
                    services.AddScoped<Greeter>().WithMiddleware<Noop>();
            }
            """);

        Assert.Equal(["KDT002"], result.DiagnosticIds);
    }

    [Fact]
    public void AnUninterceptableMethodIsReportedAsCdt003AndPassesThrough()
    {
        var result = Run("""
            public interface IParser { bool TryParse(string text, out int value); }
            public sealed class Parser : IParser
            {
                public bool TryParse(string text, out int value) => int.TryParse(text, out value);
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services) =>
                    services.AddScoped<IParser, Parser>().WithMiddleware<Noop>();
            }
            """);

        Assert.Equal(["KDT003"], result.DiagnosticIds);
        Assert.Empty(result.CompilationErrors);
        Assert.Contains("=> _target.TryParse(text, out value);", result.AllGeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnrecognisedRegistrationIsReportedAsCdt004()
    {
        var result = Run("""
            public interface IGreeter { string Greet(string name); }

            public static class Startup
            {
                public static IServiceCollection Register(this IServiceCollection services) => services;

                public static void Configure(IServiceCollection services) =>
                    services.Register().WithMiddleware<Noop>();
            }
            """);

        Assert.Equal(["KDT004"], result.DiagnosticIds);
    }

    [Fact]
    public void ATryAddRegistrationCannotBeChainedAndIsReportedAsCdt001()
    {
        // TryAdd* returns void, so the only way to write this is as two statements,
        // which the generator cannot attribute to a service.
        var result = Run("""
            public interface IGreeter { string Greet(string name); }
            public sealed class Greeter : IGreeter { public string Greet(string name) => name; }

            public static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.TryAddScoped<IGreeter, Greeter>();
                    services.WithMiddleware<Noop>();
                }
            }
            """);

        Assert.Equal(["KDT001"], result.DiagnosticIds);
        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void SkipKonduitSuppressesInterceptionWithoutADiagnostic()
    {
        var result = Run("""
            public interface IGreeter
            {
                [SkipKonduit] string Greet(string name);
            }
            public sealed class Greeter : IGreeter { public string Greet(string name) => name; }

            public static class Startup
            {
                public static void Configure(IServiceCollection services) =>
                    services.AddScoped<IGreeter, Greeter>().WithMiddleware<Noop>();
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Contains("=> _target.Greet(name);", result.AllGeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void NullableAnnotatedGenericTypesEmitValidTypeofExpressions()
    {
        var result = Run("""
            public interface ITagger
            {
                IReadOnlyList<string>? FindTags(int id);
                string? FindName(int id);
                int? FindCount(int id);
            }
            public sealed class Tagger : ITagger
            {
                public IReadOnlyList<string>? FindTags(int id) => null;
                public string? FindName(int id) => null;
                public int? FindCount(int id) => null;
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services) =>
                    services.AddScoped<ITagger, Tagger>().WithMiddleware<Noop>();
            }
            """);

        Assert.Empty(result.CompilationErrors);
        Assert.Contains("typeof(global::System.Collections.Generic.IReadOnlyList<string>)", result.AllGeneratedSource, StringComparison.Ordinal);
        Assert.Contains("typeof(int?)", result.AllGeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void RepeatedMiddlewareOnTheSameServiceGeneratesOneProxy()
    {
        var result = Run("""
            public interface IGreeter { string Greet(string name); }
            public sealed class Greeter : IGreeter { public string Greet(string name) => name; }

            public static class Startup
            {
                public static void Configure(IServiceCollection services) =>
                    services.AddScoped<IGreeter, Greeter>().WithMiddleware<Noop>().WithMiddleware<Noop>();
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        // One proxy plus the shared registrations file.
        Assert.Equal(2, result.GeneratedSources.Length);
    }

    [Fact]
    public void InheritedInterfaceMembersAreImplemented()
    {
        var result = Run("""
            public interface IAudited { string Last { get; set; } }
            public interface IGreeter : IAudited { string Greet(string name); }
            public sealed class Greeter : IGreeter
            {
                public string Last { get; set; } = "";
                public string Greet(string name) => name;
            }

            public static class Startup
            {
                public static void Configure(IServiceCollection services) =>
                    services.AddScoped<IGreeter, Greeter>().WithMiddleware<Noop>();
            }
            """);

        Assert.Empty(result.CompilationErrors);
        Assert.Contains("public string Last", result.AllGeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutAnyRegistrationNothingIsGenerated()
    {
        var result = Run("""
            public interface IGreeter { string Greet(string name); }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.GeneratedSources);
    }
}
