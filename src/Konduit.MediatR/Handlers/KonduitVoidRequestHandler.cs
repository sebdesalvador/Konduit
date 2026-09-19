using MediatR;

namespace Konduit.MediatR.Handlers;

/// <summary>
/// Runs a request handler that returns no response through a Konduit pipeline.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <remarks><see cref="KonduitContext.Result"/> stays <see langword="null"/> for these handlers.</remarks>
internal sealed class KonduitVoidRequestHandler<TRequest>(
    IRequestHandler<TRequest> inner,
    KonduitDelegate pipeline,
    IServiceProvider services)
    : IRequestHandler<TRequest>
    where TRequest : IRequest
{
    private static readonly KonduitMethod Method = HandlerMethod.For(
        typeof(IRequestHandler<TRequest>),
        "request",
        typeof(TRequest),
        typeof(Task));

    private static readonly KonduitDelegate Terminal = static async context =>
        await ((IRequestHandler<TRequest>)context.Target)
            .Handle((TRequest)context.Arguments[0]!, (CancellationToken)context.Arguments[1]!)
            .ConfigureAwait(false);

    public async Task Handle(TRequest request, CancellationToken cancellationToken)
    {
        var context = new KonduitContext(inner, Method, [request, cancellationToken], services, Terminal)
        {
            CancellationToken = cancellationToken,
        };

        await pipeline(context).ConfigureAwait(false);
    }
}
