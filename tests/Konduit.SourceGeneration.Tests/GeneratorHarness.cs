using System.Collections.Immutable;
using System.Reflection;
using Konduit.SourceGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Konduit.SourceGeneration.Tests;

/// <summary>Runs the generator over a source snippet and reports what came back.</summary>
internal static class GeneratorHarness
{
    public static GeneratorResult Run(string source)
    {
        var compilation = CSharpCompilation.Create(
            "KonduitGeneratorTests",
            [CSharpSyntaxTree.ParseText(source)],
            BuildReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver
            .Create(new KonduitProxyGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var updated, out var generatorDiagnostics);

        var runResult = driver.GetRunResult();

        return new GeneratorResult(
            generatorDiagnostics,
            [.. updated.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)],
            [.. runResult.GeneratedTrees.Select(tree => tree.ToString())]);
    }

    private static IEnumerable<MetadataReference> BuildReferences() =>
        AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Concat([typeof(global::Konduit.KonduitDelegate).Assembly, typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly])
            .Select(assembly => assembly.Location)
            .Distinct()
            .Select(location => (MetadataReference)MetadataReference.CreateFromFile(location));
}

internal sealed record GeneratorResult(
    ImmutableArray<Diagnostic> GeneratorDiagnostics,
    ImmutableArray<Diagnostic> CompilationErrors,
    ImmutableArray<string> GeneratedSources)
{
    public IEnumerable<string> DiagnosticIds => GeneratorDiagnostics.Select(d => d.Id);

    public string AllGeneratedSource => string.Join("\n", GeneratedSources);
}
