using MediatR;

namespace Konduit.MediatR.Handlers;

/// <summary>
/// Runs a streaming request handler through a Konduit pipeline.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The type of each streamed item.</typeparam>
/// <remarks>
/// The pipeline completes when the handler returns its <see cref="IAsyncEnumerable{T}"/>, before any
/// item is produced, so middleware here measures the call rather than the iteration. This matches
/// how Konduit treats <see cref="IAsyncEnumerable{T}"/> on ordinary services.
/// </remarks>
internal sealed class KonduitStreamRequestHandler<TRequest, TResponse>(
    IStreamRequestHandler<TRequest, TResponse> inner,
    KonduitDelegate pipeline,
    IServiceProvider services)
    : IStreamRequestHandler<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    private static readonly KonduitMethod Method = HandlerMethod.For(
        typeof(IStreamRequestHandler<TRequest, TResponse>),
        "request",
        typeof(TRequest),
        typeof(IAsyncEnumerable<TResponse>));

    private static readonly KonduitDelegate Terminal = static context =>
    {
        context.Result = ((IStreamRequestHandler<TRequest, TResponse>)context.Target)
            .Handle((TRequest)context.Arguments[0]!, (CancellationToken)context.Arguments[1]!);
        return default;
    };

    public IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken)
    {
        var context = new KonduitContext(inner, Method, [request, cancellationToken], services, Terminal)
        {
            CancellationToken = cancellationToken,
        };

        KonduitSync.Wait(pipeline(context));
        return (IAsyncEnumerable<TResponse>)context.Result!;
    }
}
