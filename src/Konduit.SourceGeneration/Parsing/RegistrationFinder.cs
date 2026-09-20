using System.Threading;
using System.Linq;
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
            // The second argument of AddMiddleware<TClient, TMiddleware>() is the middleware, not an
            // implementation, so nothing is inferred from it here.
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

                var (candidate, implementation) = ResolveTypes(invocation, model, cancellationToken);

                if (candidate is { TypeKind: TypeKind.Interface })
                {
                    return RegistrationMatch.Succeeded(candidate, ImplementationOf(candidate, implementation));
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
            ? RegistrationMatch.Succeeded(named, null)
            : RegistrationMatch.Failed(DiagnosticInfo.Create(
                KonduitDiagnostics.ServiceTypeMustBeAnInterface,
                location,
                named.ToDisplayString()));
    }

    /// <summary>
    /// Reads the service type, and the implementation type where the registration names one.
    /// </summary>
    /// <remarks>
    /// The implementation matters because <see cref="Konduit.SkipKonduitAttribute"/> may sit on the
    /// implementing method rather than the interface, which is the only option when the interface
    /// comes from a package you cannot edit.
    /// </remarks>
    private static (INamedTypeSymbol? Service, INamedTypeSymbol? Implementation) ResolveTypes(
        InvocationExpressionSyntax registration,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(registration, cancellationToken).Symbol is IMethodSymbol { TypeArguments.Length: > 0 } method)
        {
            return (
                method.TypeArguments[0] as INamedTypeSymbol,
                method.TypeArguments.Length > 1 ? method.TypeArguments[1] as INamedTypeSymbol : null);
        }

        // Non-generic registrations such as AddScoped(typeof(IOrderService), typeof(OrderService)).
        return (
            TypeOfArgument(registration, 0, model, cancellationToken),
            TypeOfArgument(registration, 1, model, cancellationToken));
    }

    private static INamedTypeSymbol? TypeOfArgument(
        InvocationExpressionSyntax registration,
        int index,
        SemanticModel model,
        CancellationToken cancellationToken) =>
        registration.ArgumentList.Arguments.Count > index
            && registration.ArgumentList.Arguments[index].Expression is TypeOfExpressionSyntax typeOf
                ? model.GetTypeInfo(typeOf.Type, cancellationToken).Type as INamedTypeSymbol
                : null;

    /// <summary>
    /// Accepts a candidate implementation only when it really implements the service.
    /// </summary>
    /// <remarks>
    /// A second type argument is not always an implementation — <c>AddMiddleware&lt;TClient,
    /// TMiddleware&gt;()</c> names the middleware there — so it is checked rather than assumed.
    /// </remarks>
    private static INamedTypeSymbol? ImplementationOf(INamedTypeSymbol serviceType, INamedTypeSymbol? candidate) =>
        candidate is { TypeKind: TypeKind.Class }
            && candidate.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, serviceType))
                ? candidate
                : null;
}

/// <summary>The outcome of walking one middleware chain back to its registration.</summary>
internal readonly struct RegistrationMatch(
    INamedTypeSymbol? serviceType,
    INamedTypeSymbol? implementationType,
    DiagnosticInfo? diagnostic)
{
    public INamedTypeSymbol? ServiceType { get; } = serviceType;

    /// <summary>The implementation the registration named, when it named one.</summary>
    public INamedTypeSymbol? ImplementationType { get; } = implementationType;

    public DiagnosticInfo? Diagnostic { get; } = diagnostic;

    public static RegistrationMatch Succeeded(INamedTypeSymbol serviceType, INamedTypeSymbol? implementationType) =>
        new(serviceType, implementationType, null);

    public static RegistrationMatch Failed(DiagnosticInfo? diagnostic) => new(null, null, diagnostic);
}
