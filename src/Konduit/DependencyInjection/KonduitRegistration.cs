using Konduit;
using Microsoft.Extensions.DependencyInjection;

namespace Konduit.DependencyInjection;

/// <summary>
/// The middleware chosen for one service, and how to build its proxy at resolution time.
/// </summary>
/// <remarks>
/// Middleware factories, not middleware types, are stored here: the generic
/// <c>WithMiddleware&lt;TMiddleware&gt;</c> method captures activation while the trimming annotations
/// on its type parameter are still in scope, which keeps the whole path trim- and AOT-safe.
/// </remarks>
internal sealed class KonduitRegistration(Type serviceType, Func<IServiceProvider, object> targetFactory)
{
    private readonly List<Func<IServiceProvider, KonduitDelegate, IKonduitMiddleware>> _middleware = [];

    /// <summary>Gets the descriptor this registration replaced, used to detect re-registration.</summary>
    public ServiceDescriptor? Descriptor { get; set; }

    public void Add(Func<IServiceProvider, KonduitDelegate, IKonduitMiddleware> middleware) =>
        _middleware.Add(middleware);

    /// <summary>
    /// Builds the proxy for one resolution of the service.
    /// </summary>
    /// <param name="services">The provider resolving the service, and the scope middleware resolves from.</param>
    /// <returns>The proxy to hand back to the caller.</returns>
    public object CreateProxy(IServiceProvider services)
    {
        var target = targetFactory(services);
        var builder = new KonduitPipelineBuilder();

        foreach (var middleware in _middleware)
        {
            builder.Use(next => middleware(services, next));
        }

        return KonduitProxyRegistry.Create(serviceType, target, builder.Build(), services);
    }
}
