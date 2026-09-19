using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Konduit;
using Konduit.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Adds Konduit middleware to services registered with the standard <see cref="IServiceCollection"/> methods.
/// </summary>
public static class KonduitServiceCollectionExtensions
{
    private static readonly ConditionalWeakTable<IServiceCollection, Dictionary<Type, KonduitRegistration>> Registrations = new();

    /// <summary>
    /// Wraps the service registered immediately before this call in a middleware pipeline.
    /// </summary>
    /// <typeparam name="TMiddleware">
    /// The middleware to add. It is activated from the provider that resolved the service, with the
    /// next pipeline step supplied as its <see cref="KonduitDelegate"/> constructor parameter.
    /// </typeparam>
    /// <param name="services">The collection whose most recent registration is being wrapped.</param>
    /// <returns>The same collection, so registrations and further middleware can be chained.</returns>
    /// <remarks>
    /// Chain this directly onto a registration, so it is unambiguous which service it applies to:
    /// <code>
    /// services.AddScoped&lt;IOrderService, OrderService&gt;()
    ///         .WithMiddleware&lt;LoggingMiddleware&gt;()
    ///         .WithMiddleware&lt;RetryMiddleware&gt;();
    /// </code>
    /// The first middleware added is the outermost, as with <c>app.Use(...)</c> in ASP.NET Core.
    /// The service registration keeps its original lifetime, and the implementation type stays
    /// resolvable on its own so it can be injected unwrapped where that is wanted.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Nothing was registered before this call, or the preceding registration is one Konduit cannot
    /// wrap: a non-interface service, an open generic, a keyed service, or an interface the source
    /// generator produced no proxy for.
    /// </exception>
    public static IServiceCollection WithMiddleware<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>(
        this IServiceCollection services)
        where TMiddleware : class, IKonduitMiddleware
    {
        ArgumentNullException.ThrowIfNull(services);

        if (services.Count == 0)
        {
            throw new InvalidOperationException(
                "WithMiddleware<T>() must follow a service registration, for example " +
                "services.AddScoped<IOrderService, OrderService>().WithMiddleware<LoggingMiddleware>().");
        }

        var registration = GetOrCreateRegistration(services);
        registration.Add(static (provider, next) => ActivatorUtilities.CreateInstance<TMiddleware>(provider, next));

        return services;
    }

    private static KonduitRegistration GetOrCreateRegistration(IServiceCollection services)
    {
        var index = services.Count - 1;
        var descriptor = services[index];
        var byService = Registrations.GetOrCreateValue(services);

        // A matching registration is only reusable while the descriptor it produced is still the one
        // in place; re-registering the same interface afterwards starts a fresh pipeline.
        if (byService.TryGetValue(descriptor.ServiceType, out var existing)
            && ReferenceEquals(existing.Descriptor, descriptor))
        {
            return existing;
        }

        var serviceType = Validate(descriptor);
        var registration = new KonduitRegistration(serviceType, CreateTargetFactory(services, descriptor, ref index));
        var proxyDescriptor = new ServiceDescriptor(serviceType, registration.CreateProxy, descriptor.Lifetime);

        registration.Descriptor = proxyDescriptor;
        services[index] = proxyDescriptor;
        byService[serviceType] = registration;

        return registration;
    }

    private static Type Validate(ServiceDescriptor descriptor)
    {
        if (descriptor.IsKeyedService)
        {
            throw new InvalidOperationException(
                $"Konduit cannot wrap the keyed service '{descriptor.ServiceType}'. Keyed registrations are not supported yet.");
        }

        var serviceType = descriptor.ServiceType;

        if (serviceType.IsGenericTypeDefinition)
        {
            throw new InvalidOperationException(
                $"Konduit cannot wrap '{serviceType}' because it is registered as an open generic. " +
                "Register each closed generic service separately to add middleware to it.");
        }

        if (!serviceType.IsInterface)
        {
            throw new InvalidOperationException(
                $"Konduit can only wrap services registered as an interface, but '{serviceType}' is not one. " +
                "Calls reach a proxy through the interface, so there is nothing to intercept on a concrete type.");
        }

        if (!KonduitProxyRegistry.IsRegistered(serviceType))
        {
            throw new InvalidOperationException(KonduitProxyRegistry.BuildMissingProxyMessage(serviceType));
        }

        return serviceType;
    }

    private static Func<IServiceProvider, object> CreateTargetFactory(
        IServiceCollection services,
        ServiceDescriptor descriptor,
        ref int index)
    {
        if (descriptor.ImplementationInstance is { } instance)
        {
            return _ => instance;
        }

        if (descriptor.ImplementationFactory is { } factory)
        {
            return factory;
        }

        var implementationType = descriptor.ImplementationType
            ?? throw new InvalidOperationException(
                $"Konduit cannot wrap '{descriptor.ServiceType}' because its registration has no implementation.");

        // Hand activation of the concrete type to the container rather than doing it here: it keeps
        // the trimming annotations honest, and leaves the unwrapped implementation resolvable.
        if (!ContainsServiceType(services, implementationType))
        {
            services.Insert(index, new ServiceDescriptor(implementationType, implementationType, descriptor.Lifetime));
            index++;
        }

        return provider => provider.GetRequiredService(implementationType);
    }

    private static bool ContainsServiceType(IServiceCollection services, Type serviceType)
    {
        for (var i = 0; i < services.Count; i++)
        {
            if (services[i].ServiceType == serviceType)
            {
                return true;
            }
        }

        return false;
    }
}
