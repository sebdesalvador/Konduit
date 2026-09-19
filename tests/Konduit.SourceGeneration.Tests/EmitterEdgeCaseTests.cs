namespace Konduit.SourceGeneration.Tests;

public sealed class EmitterEdgeCaseTests
{
    private const string Preamble = """
        using System;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using Konduit;
        using Microsoft.Extensions.DependencyInjection;

        public sealed class Noop : IKonduitMiddleware
        {
            public Noop(KonduitDelegate next) { }
            public ValueTask InvokeAsync(KonduitContext context) => default;
        }

        """;

    private static GeneratorResult Run(string body) => GeneratorHarness.Run(Preamble + body);

    private static string Register(string service, string implementation) => $$"""

        public static class Startup
        {
            public static void Configure(IServiceCollection services) =>
                services.AddScoped<{{service}}, {{implementation}}>().WithMiddleware<Noop>();
        }
        """;

    [Fact]
    public void RefReturningMethodsPassThroughAndAreReported()
    {
        var result = Run("""
            public interface ISlots { ref int Slot(int index); }
            public sealed class Slots : ISlots
            {
                private int[] _values = new int[8];
                public ref int Slot(int index) => ref _values[index];
            }
            """ + Register("ISlots", "Slots"));

        Assert.Equal(["KDT003"], result.DiagnosticIds);
        Assert.Empty(result.CompilationErrors);
        Assert.Contains("public ref int Slot(int index)", result.AllGeneratedSource, StringComparison.Ordinal);
        Assert.Contains("=> ref _target.Slot(index);", result.AllGeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void RefStructParametersPassThroughAndAreReported()
    {
        var result = Run("""
            public interface IWriter { int Write(Span<byte> buffer); }
            public sealed class Writer : IWriter { public int Write(Span<byte> buffer) => buffer.Length; }
            """ + Register("IWriter", "Writer"));

        Assert.Equal(["KDT003"], result.DiagnosticIds);
        Assert.Empty(result.CompilationErrors);
        Assert.Contains("=> _target.Write(buffer);", result.AllGeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void RefStructReturnsPassThroughAndAreReported()
    {
        var result = Run("""
            public interface IReader { ReadOnlySpan<byte> Read(int count); }
            public sealed class Reader : IReader
            {
                public ReadOnlySpan<byte> Read(int count) => default;
            }
            """ + Register("IReader", "Reader"));

        Assert.Equal(["KDT003"], result.DiagnosticIds);
        Assert.Empty(result.CompilationErrors);
    }

    [Fact]
    public void InParametersAreStillIntercepted()
    {
        var result = Run("""
            public interface IAdder { int Add(in int left, in int right); }
            public sealed class Adder : IAdder { public int Add(in int left, in int right) => left + right; }
            """ + Register("IAdder", "Adder"));

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
        Assert.Contains("public int Add(in int left, in int right)", result.AllGeneratedSource, StringComparison.Ordinal);
        Assert.Contains("context.Arguments[0]!, (int)context.Arguments[1]!", result.AllGeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SettableMembersAreForwardedAndInitOnlyOnesAreReported()
    {
        var result = Run("""
            public interface ISettings
            {
                string this[string key] { get; set; }
                string Name { get; init; }
                int Count { set; }
            }
            public sealed class Settings : ISettings
            {
                public string this[string key] { get => key; set { } }
                public string Name { get; init; } = "";
                public int Count { set { } }
            }
            """ + Register("ISettings", "Settings"));

        Assert.Empty(result.CompilationErrors);
        Assert.Equal(["KDT006"], result.DiagnosticIds);
        Assert.Contains("set => _target[key] = value;", result.AllGeneratedSource, StringComparison.Ordinal);
        Assert.Contains("set => _target.Count = value;", result.AllGeneratedSource, StringComparison.Ordinal);
        Assert.Contains("init => throw new global::System.NotSupportedException", result.AllGeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryConstraintKindIsRepeatedOnTheGeneratedMethod()
    {
        var result = Run("""
            public interface IConverter
            {
                T ToReference<T>(string text) where T : class;
                T ToValue<T>(string text) where T : struct;
                T ToUnmanaged<T>(string text) where T : unmanaged;
                T ToNotNull<T>(string text) where T : notnull;
                T ToComparable<T>(string text) where T : IComparable<T>, new();
            }
            public sealed class Converter : IConverter
            {
                public T ToReference<T>(string text) where T : class => default!;
                public T ToValue<T>(string text) where T : struct => default;
                public T ToUnmanaged<T>(string text) where T : unmanaged => default;
                public T ToNotNull<T>(string text) where T : notnull => default!;
                public T ToComparable<T>(string text) where T : IComparable<T>, new() => new();
            }
            """ + Register("IConverter", "Converter"));

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        foreach (var constraint in new[] { "where T : class", "where T : struct", "where T : unmanaged", "where T : notnull" })
        {
            Assert.Contains(constraint, result.AllGeneratedSource, StringComparison.Ordinal);
        }

        Assert.Contains("where T : global::System.IComparable<T>, new()", result.AllGeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ParameterlessMethodsUseAnEmptyArgumentArray()
    {
        var result = Run("""
            public interface IClock { DateTimeOffset Now(); }
            public sealed class Clock : IClock { public DateTimeOffset Now() => DateTimeOffset.UtcNow; }
            """ + Register("IClock", "Clock"));

        Assert.Empty(result.CompilationErrors);
        Assert.Contains("global::System.Array.Empty<object?>()", result.AllGeneratedSource, StringComparison.Ordinal);
        Assert.Contains("global::System.Array.Empty<global::Konduit.KonduitParameter>()", result.AllGeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultInterfaceMembersAreLeftToInterfaceDispatch()
    {
        var result = Run("""
            public interface IGreeter
            {
                string Greet(string name);
                string GreetTwice(string name) => Greet(name) + Greet(name);
            }
            public sealed class Greeter : IGreeter { public string Greet(string name) => name; }
            """ + Register("IGreeter", "Greeter"));

        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain("GreetTwice", result.AllGeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ClosedGenericServicesGetTheirOwnProxy()
    {
        var result = Run("""
            public interface IRepository<T> { T? Find(int id); }
            public sealed class OrderRepository : IRepository<string>
            {
                public string? Find(int id) => null;
            }
            """ + Register("IRepository<string>", "OrderRepository"));

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
        Assert.Contains("IRepository_stringKonduitProxy", result.AllGeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyInterfaceStillProducesACompilableProxy()
    {
        var result = Run("""
            public interface IMarker { }
            public sealed class Marker : IMarker { }
            """ + Register("IMarker", "Marker"));

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
    }
}
