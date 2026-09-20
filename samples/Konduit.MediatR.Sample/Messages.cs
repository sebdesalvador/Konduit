using Konduit;
using MediatR;

namespace Konduit.MediatR.Sample;

// A query, a command, and an event: the three shapes MediatR dispatches.

public sealed record GetOrder(int Id) : IRequest<string>;

public sealed class GetOrderHandler : IRequestHandler<GetOrder, string>
{
    public async Task<string> Handle(GetOrder request, CancellationToken cancellationToken)
    {
        await Task.Delay(15, cancellationToken);
        return $"order #{request.Id}";
    }
}

public sealed record CancelOrder(int Id) : IRequest;

public sealed class CancelOrderHandler : IRequestHandler<CancelOrder>
{
    public Task Handle(CancelOrder request, CancellationToken cancellationToken)
    {
        Console.WriteLine($"      cancelling order #{request.Id}");
        return Task.CompletedTask;
    }
}

public sealed record OrderPlaced(int Id) : INotification;

public sealed class SendEmail : INotificationHandler<OrderPlaced>
{
    public Task Handle(OrderPlaced notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"      emailing about order #{notification.Id}");
        return Task.CompletedTask;
    }
}

public sealed class WriteAudit : INotificationHandler<OrderPlaced>
{
    public Task Handle(OrderPlaced notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"      auditing order #{notification.Id}");
        return Task.CompletedTask;
    }
}

// Marked on the message, so this one is never instrumented wherever it is handled.
[SkipKonduit]
public sealed record Heartbeat : IRequest<string>;

public sealed class HeartbeatHandler : IRequestHandler<Heartbeat, string>
{
    public Task<string> Handle(Heartbeat request, CancellationToken cancellationToken) =>
        Task.FromResult("alive");
}
