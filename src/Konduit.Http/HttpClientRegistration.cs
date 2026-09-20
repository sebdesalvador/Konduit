using Microsoft.Extensions.DependencyInjection;

namespace Konduit.Http;

/// <summary>
/// The middleware chosen for one typed client, and how to build its proxy at resolution time.
/// </summary>
/// <remarks>
/// Middleware factories, not middleware types, are stored here: the generic <c>AddMiddleware</c>
/// method captures activation while the trimming annotations on its type parameter are still in
/// scope, which keeps the whole path trim- and AOT-safe.
/// </remarks>
internal sealed class HttpClientRegistration(Type serviceType, Func<IServiceProvider, object> targetFactory)
{
    private readonly List<Func<IServiceProvider, KonduitDelegate, IKonduitMiddleware>> _middleware = [];

    /// <summary>Gets the service type this registration wraps.</summary>
    public Type ServiceType { get; } = serviceType;

    public void Add(Func<IServiceProvider, KonduitDelegate, IKonduitMiddleware> middleware) =>
        _middleware.Add(middleware);

    /// <summary>Builds the proxy for one resolution of the typed client.</summary>
    public object CreateProxy(IServiceProvider services)
    {
        var target = targetFactory(services);
        var builder = new KonduitPipelineBuilder();

        foreach (var middleware in _middleware)
        {
            builder.Use(next => middleware(services, next));
        }

        return KonduitProxyRegistry.Create(ServiceType, target, builder.Build(), services);
    }
}
