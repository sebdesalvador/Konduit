namespace Konduit;

/// <summary>
/// Creates a generated proxy around a service implementation.
/// </summary>
/// <param name="target">The implementation to wrap.</param>
/// <param name="pipeline">The middleware pipeline every intercepted call runs through.</param>
/// <param name="services">The provider that resolved the service.</param>
/// <returns>A proxy implementing the service interface.</returns>
public delegate object KonduitProxyFactory(object target, KonduitDelegate pipeline, IServiceProvider services);
