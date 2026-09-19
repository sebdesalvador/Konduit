using MediatR;

namespace Konduit.MediatR.Handlers;

/// <summary>
/// Runs a notification handler through a Konduit pipeline.
/// </summary>
/// <typeparam name="TNotification">The notification type.</typeparam>
/// <typeparam name="THandler">
/// The concrete handler being wrapped. It is not used by the implementation; it exists to give each
/// wrapped handler a distinct closed type.
/// </typeparam>
/// <remarks>
/// The second type parameter is load-bearing despite being unused. MediatR distinguishes the
/// subscribers of a notification by their concrete type, so wrapping every handler in one shared
/// decorator type would collapse them and silently drop all but one. Closing the decorator over the
/// handler type keeps <c>EmailOnPlaced</c> and <c>AuditOnPlaced</c> distinct, and both still run.
/// <para>
/// Each subscriber is wrapped separately, so publishing a notification with three handlers runs the
/// pipeline three times, once around each handler.
/// </para>
/// </remarks>
internal sealed class KonduitNotificationHandler<TNotification, THandler>(
    THandler inner,
    KonduitDelegate pipeline,
    IServiceProvider services)
    : INotificationHandler<TNotification>
    where TNotification : INotification
    where THandler : INotificationHandler<TNotification>
{
    private static readonly KonduitMethod Method = HandlerMethod.For(
        typeof(INotificationHandler<TNotification>),
        "notification",
        typeof(TNotification),
        typeof(Task));

    private static readonly KonduitDelegate Terminal = static async context =>
        await ((INotificationHandler<TNotification>)context.Target)
            .Handle((TNotification)context.Arguments[0]!, (CancellationToken)context.Arguments[1]!)
            .ConfigureAwait(false);

    public async Task Handle(TNotification notification, CancellationToken cancellationToken)
    {
        var context = new KonduitContext(inner, Method, [notification, cancellationToken], services, Terminal)
        {
            CancellationToken = cancellationToken,
        };

        await pipeline(context).ConfigureAwait(false);
    }
}
