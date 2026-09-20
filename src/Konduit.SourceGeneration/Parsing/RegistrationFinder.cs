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

    public const string AddMiddlewareName = "AddMiddleware";

    /// <summary>Reports whether a method name is one of Konduit's middleware-adding extensions.</summary>
    /// <remarks>
    /// <c>WithMiddleware</c> is chained onto an <c>IServiceCollection</c> registration;
    /// <c>AddMiddleware</c> onto builders such as <c>IHttpClientBuilder</c>, whose own extensions
    /// are named with an <c>Add</c> prefix.
    /// </remarks>
    public static bool IsMiddlewareCallName(string name) =>
        name is WithMiddlewareName or AddMiddlewareName;

    public static RegistrationMatch Find(
        InvocationExpressionSyntax withMiddleware,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (withMiddleware.Expression is not MemberAccessExpressionSyntax access)
        {
            return RegistrationMatch.Failed(null);
        }

        // An overload that names the service itself, such as
        // AddMiddleware<IOrderService, LoggingMiddleware>(), says which service it applies to and
        // needs no walking back through the chain.
        if (access.Name is GenericNameSyntax { TypeArgumentList.Arguments.Count: 2 }
            && model.GetSymbolInfo(withMiddleware, cancellationToken).Symbol is IMethodSymbol { TypeArguments.Length: 2 } explicitCall)
        {
            return Validate(explicitCall.TypeArguments[0], withMiddleware.GetLocation());
        }

        // Walk back through the chain looking for the call that named the service.
        //
        // Intermediate calls are skipped rather than treated as the registration, because builders
        // such as IHttpClientBuilder are configured in the middle of the chain:
        //
        //     services.AddHttpClient<IOrderApi, OrderApi>(...)
        //             .ConfigurePrimaryHttpMessageHandler(...)   <- no service type
        //             .AddHttpMessageHandler<AuthHandler>()      <- a type, but not the service
        //             .AddMiddleware<LoggingMiddleware>();
        //
        // so the first interface found walking backwards is the service being registered.
        var current = access.Expression;
        var sawRegistrationCall = false;
        INamedTypeSymbol? nonInterface = null;
        Location? nonInterfaceLocation = null;

        while (current is InvocationExpressionSyntax invocation
            && invocation.Expression is MemberAccessExpressionSyntax inner)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsMiddlewareCallName(inner.Name.Identifier.ValueText))
            {
                sawRegistrationCall = true;

                var candidate = ResolveServiceType(invocation, model, cancellationToken);

                if (candidate is { TypeKind: TypeKind.Interface })
                {
                    return RegistrationMatch.Succeeded(candidate);
                }

                if (candidate is not null && nonInterface is null)
                {
                    nonInterface = candidate;
                    nonInterfaceLocation = invocation.GetLocation();
                }
            }

            current = inner.Expression;
        }

        // A concrete type was registered, as in services.AddScoped<OrderService>().
        if (nonInterface is not null)
        {
            return RegistrationMatch.Failed(DiagnosticInfo.Create(
                KonduitDiagnostics.ServiceTypeMustBeAnInterface,
                nonInterfaceLocation,
                nonInterface.ToDisplayString()));
        }

        return RegistrationMatch.Failed(DiagnosticInfo.Create(
            sawRegistrationCall ? KonduitDiagnostics.UnknownServiceType : KonduitDiagnostics.NotChainedToRegistration,
            access.Name.GetLocation()));
    }

    private static RegistrationMatch Validate(ITypeSymbol? serviceType, Location location)
    {
        if (serviceType is not INamedTypeSymbol named)
        {
            return RegistrationMatch.Failed(DiagnosticInfo.Create(KonduitDiagnostics.UnknownServiceType, location));
        }

        return named.TypeKind == TypeKind.Interface
            ? RegistrationMatch.Succeeded(named)
            : RegistrationMatch.Failed(DiagnosticInfo.Create(
                KonduitDiagnostics.ServiceTypeMustBeAnInterface,
                location,
                named.ToDisplayString()));
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
