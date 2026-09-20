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
            Expression: MemberAccessExpressionSyntax { Name: GenericNameSyntax name },
        }
        && RegistrationFinder.IsMiddlewareCallName(name.Identifier.ValueText);

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
            return new ProxyCandidate(null, null, EquatableArray<DiagnosticInfo>.From(diagnostics));
        }

        var known = new KnownTypes(context.SemanticModel.Compilation);
        var location = invocation.GetLocation();

        var shared = ProxySpecFactory.Create(match.ServiceType, null, known, location, diagnostics, cancellationToken);
        ProxySpec? specialised = null;

        if (match.ImplementationType is { } implementation)
        {
            // The diagnostics come from the interface's own members, so they would be identical for
            // both specs; collecting them twice would report each one twice.
            var duplicated = new List<DiagnosticInfo>();

            var candidate = ProxySpecFactory.Create(
                match.ServiceType,
                implementation,
                known,
                location,
                duplicated,
                cancellationToken);

            // A second proxy only earns its place when the implementation skips something the
            // interface does not; otherwise the shared one already describes this registration.
            if (!candidate.Methods.Equals(shared.Methods))
            {
                specialised = candidate;
            }
        }

        return new ProxyCandidate(
            shared,
            specialised,
            EquatableArray<DiagnosticInfo>.From(diagnostics),
            known.HasModuleInitializer);
    }

    private const string MiddlewareInterface = "Konduit.IKonduitMiddleware";

    /// <summary>
    /// Reports whether a call is one of Konduit's middleware-adding extensions.
    /// </summary>
    /// <remarks>
    /// Identified by two things rather than by the declaring class, so companion packages can add
    /// their own entry points — such as Konduit.Http's <c>AddMiddleware</c> on
    /// <c>IHttpClientBuilder</c> — without the generator knowing about them.
    /// <para>
    /// It must be an <b>extension method</b> whose type parameter is constrained to
    /// <c>IKonduitMiddleware</c>. The extension requirement matters: middleware is also added
    /// through instance methods on builders that need no generated proxy at all, such as
    /// <c>KonduitMediatRBuilder.WithMiddleware&lt;T&gt;()</c>, whose handlers are wrapped by
    /// hand-written decorators. Matching those would demand a service type that the chain does not
    /// contain, and fail the build.
    /// </para>
    /// </remarks>
    private static bool IsKonduitExtension(IMethodSymbol method)
    {
        var definition = method.ReducedFrom ?? method.OriginalDefinition;

        if (!definition.IsExtensionMethod)
        {
            return false;
        }

        foreach (var parameter in definition.TypeParameters)
        {
            foreach (var constraint in parameter.ConstraintTypes)
            {
                if (constraint.ToDisplayString() == MiddlewareInterface)
                {
                    return true;
                }
            }
        }

        return false;
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

            Collect(candidate.Spec);
            Collect(candidate.SpecialisedSpec);

            void Collect(ProxySpec? spec)
            {
                // Keyed by both types: one interface can have a shared proxy and a specialised one
                // per implementation that skips extra methods.
                if (spec is not null && seen.Add(spec.InterfaceFullyQualified + " / " + spec.ImplementationForTypeOf))
                {
                    specs.Add(spec);
                }
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

/// <summary>One middleware call site, resolved to the proxies it needs.</summary>
/// <param name="Spec">The proxy serving any implementation of the interface.</param>
/// <param name="SpecialisedSpec">
/// A proxy for the implementation this registration named, emitted only when that implementation
/// marks methods the interface does not.
/// </param>
/// <param name="Diagnostics">Diagnostics to report for this call site.</param>
/// <param name="HasModuleInitializer">Whether the compilation defines ModuleInitializerAttribute.</param>
internal sealed record ProxyCandidate(
    ProxySpec? Spec,
    ProxySpec? SpecialisedSpec,
    EquatableArray<DiagnosticInfo> Diagnostics,
    bool HasModuleInitializer = true);
