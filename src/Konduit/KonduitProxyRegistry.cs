using System.Collections.Concurrent;

namespace Konduit;

/// <summary>
/// Maps service interfaces to their generated proxy factories.
/// </summary>
/// <remarks>
/// The source generator emits a <see cref="System.Runtime.CompilerServices.ModuleInitializerAttribute"/>
/// that fills this registry, so it is populated before any registration code in that assembly runs.
/// </remarks>
public static class KonduitProxyRegistry
{
    private static readonly ConcurrentDictionary<Type, KonduitProxyFactory> Factories = new();

    /// <summary>
    /// Registers the proxy factory for a service interface. Called by generated module initializers.
    /// </summary>
    /// <param name="serviceType">The service interface the proxy implements.</param>
    /// <param name="factory">Creates the proxy.</param>
    /// <remarks>Registering the same interface twice is harmless; the first factory wins.</remarks>
    public static void Register(Type serviceType, KonduitProxyFactory factory)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(factory);

        Factories.TryAdd(serviceType, factory);
    }

    /// <summary>
    /// Reports whether a proxy has been generated for a service interface.
    /// </summary>
    /// <param name="serviceType">The service interface to look for.</param>
    /// <returns><see langword="true"/> when a proxy factory is registered.</returns>
    public static bool IsRegistered(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        return Factories.ContainsKey(serviceType);
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
        IServiceProvider services) =>
        Factories.TryGetValue(serviceType, out var factory)
            ? factory(target, pipeline, services)
            : throw new InvalidOperationException(BuildMissingProxyMessage(serviceType));

    internal static string BuildMissingProxyMessage(Type serviceType) =>
        $"Konduit has no generated proxy for '{serviceType}'. The source generator emits one for each " +
        "service registered in a chain ending in WithMiddleware<T>(); check the build output for Konduit " +
        "diagnostics, and make sure the registration and the WithMiddleware<T>() call are a single expression.";
}
