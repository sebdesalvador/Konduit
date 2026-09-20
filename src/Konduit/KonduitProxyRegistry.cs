using System.Collections.Concurrent;

namespace Konduit;

/// <summary>
/// Maps service interfaces to their generated proxy factories.
/// </summary>
/// <remarks>
/// The source generator emits a <c>[ModuleInitializer]</c>
/// that fills this registry, so it is populated before any registration code in that assembly runs.
/// </remarks>
public static class KonduitProxyRegistry
{
    // Keyed by service type and, for a proxy specialised to one implementation, that implementation.
    // A service registered behind two implementations that skip different methods needs a proxy each.
    private static readonly ConcurrentDictionary<(Type Service, Type? Implementation), KonduitProxyFactory> Factories = new();

    /// <summary>
    /// Registers the proxy factory for a service interface. Called by generated module initializers.
    /// </summary>
    /// <param name="serviceType">The service interface the proxy implements.</param>
    /// <param name="factory">Creates the proxy.</param>
    /// <remarks>Registering the same interface twice is harmless; the first factory wins.</remarks>
    public static void Register(Type serviceType, KonduitProxyFactory factory)
    {
        Throw.IfNull(serviceType);
        Throw.IfNull(factory);

        Factories.TryAdd((serviceType, null), factory);
    }

    /// <summary>
    /// Registers a proxy factory specialised to one implementation of a service interface.
    /// </summary>
    /// <param name="serviceType">The service interface the proxy implements.</param>
    /// <param name="implementationType">The implementation this proxy is specialised for.</param>
    /// <param name="factory">Creates the proxy.</param>
    /// <remarks>
    /// The generator emits one of these when an implementation marks methods with
    /// <see cref="SkipKonduitAttribute"/> that the interface does not, since a proxy shared by every
    /// implementation could not honour them.
    /// </remarks>
    public static void Register(Type serviceType, Type implementationType, KonduitProxyFactory factory)
    {
        Throw.IfNull(serviceType);
        Throw.IfNull(implementationType);
        Throw.IfNull(factory);

        Factories.TryAdd((serviceType, implementationType), factory);
    }

    /// <summary>
    /// Reports whether a proxy has been generated for a service interface.
    /// </summary>
    /// <param name="serviceType">The service interface to look for.</param>
    /// <returns><see langword="true"/> when a proxy factory is registered.</returns>
    public static bool IsRegistered(Type serviceType)
    {
        Throw.IfNull(serviceType);

        return Factories.ContainsKey((serviceType, null));
    }

    /// <summary>
    /// Creates the generated proxy for a service interface.
    /// </summary>
    /// <param name="serviceType">The service interface to create a proxy for.</param>
    /// <param name="target">The implementation the proxy wraps.</param>
    /// <param name="pipeline">The middleware pipeline every intercepted call runs through.</param>
    /// <param name="services">The provider that resolved the service.</param>
    /// <returns>A proxy implementing <paramref name="serviceType"/>.</returns>
    /// <remarks>
    /// Registration extensions use this to wrap a service once they have decided which descriptor to
    /// replace. Companion packages such as Konduit.Http build on it to support registration shapes
    /// the core package does not know about.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// No proxy was generated for <paramref name="serviceType"/>.
    /// </exception>
    public static object Create(
        Type serviceType,
        object target,
        KonduitDelegate pipeline,
        IServiceProvider services)
    {
        Throw.IfNull(target);

        // A proxy specialised to this implementation wins over the shared one. The lookup is by
        // type identity alone, so nothing is reflected over and the call path stays allocation-free.
        if (Factories.TryGetValue((serviceType, target.GetType()), out var specialised))
        {
            return specialised(target, pipeline, services);
        }

        return Factories.TryGetValue((serviceType, null), out var factory)
            ? factory(target, pipeline, services)
            : throw new InvalidOperationException(BuildMissingProxyMessage(serviceType));
    }

    internal static string BuildMissingProxyMessage(Type serviceType) =>
        $"Konduit has no generated proxy for '{serviceType}'. The source generator emits one for each " +
        "service registered in a chain ending in WithMiddleware<T>(); check the build output for Konduit " +
        "diagnostics, and make sure the registration and the WithMiddleware<T>() call are a single expression.";
}
