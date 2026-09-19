using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Konduit.SourceGeneration.Parsing;

/// <summary>
/// Walks back from a <c>WithMiddleware&lt;T&gt;()</c> call to the registration it decorates.
/// </summary>
/// <remarks>
/// The chain is read syntactically because <see cref="Microsoft.Extensions.DependencyInjection.IServiceCollection"/>
/// carries no type information about the service just registered; the service type only exists as a
/// type argument on the registration call itself.
/// </remarks>
internal static class RegistrationFinder
{
    public const string WithMiddlewareName = "WithMiddleware";

    public static RegistrationMatch Find(
        InvocationExpressionSyntax withMiddleware,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (withMiddleware.Expression is not MemberAccessExpressionSyntax access)
        {
            return RegistrationMatch.Failed(null);
        }

        var current = access.Expression;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (current is not InvocationExpressionSyntax invocation
                || invocation.Expression is not MemberAccessExpressionSyntax inner)
            {
                return RegistrationMatch.Failed(
                    DiagnosticInfo.Create(KonduitDiagnostics.NotChainedToRegistration, access.Name.GetLocation()));
            }

            var name = inner.Name.Identifier.ValueText;

            if (name == WithMiddlewareName)
            {
                current = inner.Expression;
                continue;
            }

            return Extract(invocation, model, cancellationToken);
        }
    }

    private static RegistrationMatch Extract(
        InvocationExpressionSyntax registration,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var location = registration.GetLocation();

        var serviceType = ResolveServiceType(registration, model, cancellationToken);

        if (serviceType is null)
        {
            return RegistrationMatch.Failed(DiagnosticInfo.Create(KonduitDiagnostics.UnknownServiceType, location));
        }

        if (serviceType.TypeKind != TypeKind.Interface)
        {
            return RegistrationMatch.Failed(DiagnosticInfo.Create(
                KonduitDiagnostics.ServiceTypeMustBeAnInterface,
                location,
                serviceType.ToDisplayString()));
        }

        return RegistrationMatch.Succeeded(serviceType);
    }

    private static INamedTypeSymbol? ResolveServiceType(
        InvocationExpressionSyntax registration,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(registration, cancellationToken).Symbol is IMethodSymbol { TypeArguments.Length: > 0 } method)
        {
            return method.TypeArguments[0] as INamedTypeSymbol;
        }

        // Non-generic registrations such as AddScoped(typeof(IOrderService), typeof(OrderService)).
        var first = registration.ArgumentList.Arguments.Count > 0
            ? registration.ArgumentList.Arguments[0].Expression
            : null;

        return first is TypeOfExpressionSyntax typeOf
            ? model.GetTypeInfo(typeOf.Type, cancellationToken).Type as INamedTypeSymbol
            : null;
    }
}

/// <summary>The outcome of walking one <c>WithMiddleware&lt;T&gt;()</c> chain.</summary>
internal readonly struct RegistrationMatch(INamedTypeSymbol? serviceType, DiagnosticInfo? diagnostic)
{
    public INamedTypeSymbol? ServiceType { get; } = serviceType;

    public DiagnosticInfo? Diagnostic { get; } = diagnostic;

    public static RegistrationMatch Succeeded(INamedTypeSymbol serviceType) => new(serviceType, null);

    public static RegistrationMatch Failed(DiagnosticInfo? diagnostic) => new(null, diagnostic);
}
