using Microsoft.CodeAnalysis;

namespace Konduit.SourceGeneration.Parsing;

/// <summary>Types the generator compares against, resolved once per compilation.</summary>
internal sealed class KnownTypes(Compilation compilation)
{
    public INamedTypeSymbol? Task { get; } = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");

    public INamedTypeSymbol? TaskOfT { get; } = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");

    public INamedTypeSymbol? ValueTask { get; } = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask");

    public INamedTypeSymbol? ValueTaskOfT { get; } = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1");

    public INamedTypeSymbol? CancellationToken { get; } = compilation.GetTypeByMetadataName("System.Threading.CancellationToken");

    public INamedTypeSymbol? SkipKonduitAttribute { get; } = compilation.GetTypeByMetadataName("Konduit.SkipKonduitAttribute");

    public bool HasModuleInitializer { get; } =
        compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.ModuleInitializerAttribute") is not null;
}
