using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Konduit.SourceGeneration.Emitting;
using Konduit.SourceGeneration.Model;
using Konduit.SourceGeneration.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Konduit.SourceGeneration;

/// <summary>
/// Emits a proxy class for every service wrapped with <c>WithMiddleware&lt;T&gt;()</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class KonduitProxyGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => IsWithMiddlewareCall(node), Transform)
            .Where(static candidate => candidate is not null)
            .Select(static (candidate, _) => candidate!);

        context.RegisterSourceOutput(candidates.Collect(), Execute);
    }

    private static bool IsWithMiddlewareCall(SyntaxNode node) =>
        node is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax
            {
                Name: GenericNameSyntax { Identifier.ValueText: RegistrationFinder.WithMiddlewareName },
            },
        };

    private static ProxyCandidate? Transform(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (context.SemanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method
            || !IsKonduitExtension(method))
        {
            return null;
        }

        var match = RegistrationFinder.Find(invocation, context.SemanticModel, cancellationToken);
        var diagnostics = new List<DiagnosticInfo>();

        if (match.Diagnostic is not null)
        {
            diagnostics.Add(match.Diagnostic);
        }

        if (match.ServiceType is null)
        {
            return new ProxyCandidate(null, EquatableArray<DiagnosticInfo>.From(diagnostics));
        }

        var known = new KnownTypes(context.SemanticModel.Compilation);
        var spec = ProxySpecFactory.Create(
            match.ServiceType,
            known,
            invocation.GetLocation(),
            diagnostics,
            cancellationToken);

        return new ProxyCandidate(spec, EquatableArray<DiagnosticInfo>.From(diagnostics), known.HasModuleInitializer);
    }

    private static bool IsKonduitExtension(IMethodSymbol method)
    {
        var definition = method.ReducedFrom ?? method.OriginalDefinition;

        return definition.ContainingType?.Name == "KonduitServiceCollectionExtensions"
            && definition.ContainingType.ContainingNamespace.ToDisplayString() == "Microsoft.Extensions.DependencyInjection";
    }

    private static void Execute(SourceProductionContext context, ImmutableArray<ProxyCandidate> candidates)
    {
        var specs = new List<ProxySpec>();
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        var needsPolyfill = false;

        foreach (var candidate in candidates)
        {
            foreach (var diagnostic in candidate.Diagnostics)
            {
                context.ReportDiagnostic(diagnostic.ToDiagnostic());
            }

            if (candidate.Spec is null)
            {
                continue;
            }

            needsPolyfill |= !candidate.HasModuleInitializer;

            if (seen.Add(candidate.Spec.InterfaceFullyQualified))
            {
                specs.Add(candidate.Spec);
            }
        }

        if (specs.Count == 0)
        {
            return;
        }

        foreach (var spec in specs)
        {
            context.AddSource($"{spec.ProxyTypeName}.g.cs", ProxyEmitter.Emit(spec));
        }

        context.AddSource("KonduitProxyRegistrations.g.cs", RegistrationsEmitter.Emit(specs, needsPolyfill));
    }
}

/// <summary>One <c>WithMiddleware&lt;T&gt;()</c> call site, resolved to a proxy to emit.</summary>
internal sealed record ProxyCandidate(
    ProxySpec? Spec,
    EquatableArray<DiagnosticInfo> Diagnostics,
    bool HasModuleInitializer = true);
