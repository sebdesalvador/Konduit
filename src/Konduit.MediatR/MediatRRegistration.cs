using Microsoft.Extensions.DependencyInjection;

namespace Konduit.MediatR;

/// <summary>
/// The middleware chosen for every MediatR handler, shared by all of them.
/// </summary>
/// <remarks>
/// Handlers are wrapped when <c>AddKonduitToMediatRHandlers</c> runs, but middleware is added
/// afterwards by chained <c>WithMiddleware</c> calls. The wrapped handlers hold this object rather
/// than a snapshot of the list, so middleware added later still applies.
/// </remarks>
internal sealed class MediatRRegistration
{
    private readonly List<Func<IServiceProvider, KonduitDelegate, IKonduitMiddleware>> _middleware = [];

    public void Add(Func<IServiceProvider, KonduitDelegate, IKonduitMiddleware> middleware) =>
        _middleware.Add(middleware);

    /// <summary>Builds the pipeline for one handler resolution, from the resolving scope.</summary>
    public KonduitDelegate BuildPipeline(IServiceProvider services)
    {
        var builder = new KonduitPipelineBuilder();

        foreach (var middleware in _middleware)
        {
            builder.Use(next => middleware(services, next));
        }

        return builder.Build();
    }
}
