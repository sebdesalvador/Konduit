using MediatR;

namespace Konduit.MediatR.Handlers;

/// <summary>
/// Runs a request handler that returns a response through a Konduit pipeline.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
internal sealed class KonduitRequestHandler<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner,
    KonduitDelegate pipeline,
    IServiceProvider services)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly KonduitMethod Method = HandlerMethod.For(
        typeof(IRequestHandler<TRequest, TResponse>),
        "request",
        typeof(TRequest),
        typeof(Task<TResponse>));

    private static readonly KonduitDelegate Terminal = static async context =>
        context.Result = await ((IRequestHandler<TRequest, TResponse>)context.Target)
            .Handle((TRequest)context.Arguments[0]!, (CancellationToken)context.Arguments[1]!)
            .ConfigureAwait(false);

    public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken)
    {
        var context = new KonduitContext(inner, Method, [request, cancellationToken], services, Terminal)
        {
            CancellationToken = cancellationToken,
        };

        await pipeline(context).ConfigureAwait(false);
        return (TResponse)context.Result!;
    }
}
