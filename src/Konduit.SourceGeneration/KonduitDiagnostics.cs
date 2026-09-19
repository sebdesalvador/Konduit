using Microsoft.CodeAnalysis;

namespace Konduit.SourceGeneration;

/// <summary>The diagnostics the Konduit generator can report.</summary>
internal static class KonduitDiagnostics
{
    private const string Category = "Konduit";

    public static readonly DiagnosticDescriptor NotChainedToRegistration = new(
        "KDT001",
        "WithMiddleware<T>() must be chained onto a service registration",
        "Konduit cannot tell which service this middleware belongs to. Chain WithMiddleware<T>() directly onto " +
        "the registration, as in services.AddScoped<IOrderService, OrderService>().WithMiddleware<T>().",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ServiceTypeMustBeAnInterface = new(
        "KDT002",
        "Konduit can only add middleware to services registered as an interface",
        "'{0}' is not an interface. Calls reach a Konduit proxy through the service interface, so there is " +
        "nothing to intercept on a concrete type.",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MethodBypassesPipeline = new(
        "KDT003",
        "Method cannot be intercepted and will bypass the Konduit pipeline",
        "'{0}' {1}, so calls to it are forwarded straight to the implementation and run no middleware",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InitOnlyPropertyCannotBeForwarded = new(
        "KDT006",
        "Init-only interface properties cannot be forwarded by a Konduit proxy",
        "'{0}' is init-only, and a proxy cannot assign an init-only member of the implementation it wraps. The " +
        "generated accessor throws; give the interface a normal setter, or set the value on the implementation.",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnknownServiceType = new(
        "KDT004",
        "Konduit could not determine the service type of this registration",
        "Konduit could not work out which service this registration adds. Use a generic registration such as " +
        "AddScoped<IOrderService, OrderService>(), or AddScoped(typeof(IOrderService), typeof(OrderService)).",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
