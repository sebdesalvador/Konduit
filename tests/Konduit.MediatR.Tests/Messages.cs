using Konduit;
using MediatR;

namespace Konduit.MediatR.Tests;

public sealed record Describe(int Id) : IRequest<string>;

public sealed class DescribeHandler(Recorder recorder) : IRequestHandler<Describe, string>
{
    public Task<string> Handle(Describe request, CancellationToken cancellationToken)
    {
        recorder.Calls.Add($"DescribeHandler({request.Id})");
        return Task.FromResult($"order {request.Id}");
    }
}

public sealed record Cancel(int Id) : IRequest;

public sealed class CancelHandler(Recorder recorder) : IRequestHandler<Cancel>
{
    public Task Handle(Cancel request, CancellationToken cancellationToken)
    {
        recorder.Calls.Add($"CancelHandler({request.Id})");
        return Task.CompletedTask;
    }
}

public sealed record Placed(int Id) : INotification;

public sealed class EmailOnPlaced(Recorder recorder) : INotificationHandler<Placed>
{
    public Task Handle(Placed notification, CancellationToken cancellationToken)
    {
        recorder.Calls.Add($"EmailOnPlaced({notification.Id})");
        return Task.CompletedTask;
    }
}

public sealed class AuditOnPlaced(Recorder recorder) : INotificationHandler<Placed>
{
    public Task Handle(Placed notification, CancellationToken cancellationToken)
    {
        recorder.Calls.Add($"AuditOnPlaced({notification.Id})");
        return Task.CompletedTask;
    }
}

public sealed record StreamItems(int Count) : IStreamRequest<int>;

public sealed class StreamItemsHandler(Recorder recorder) : IStreamRequestHandler<StreamItems, int>
{
    public async IAsyncEnumerable<int> Handle(
        StreamItems request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var i = 0; i < request.Count; i++)
        {
            await Task.Yield();
            recorder.Calls.Add($"yield({i})");
            yield return i;
        }
    }
}

// --- skip variants -------------------------------------------------------

public sealed record SkippedByHandlerClass(int Id) : IRequest<string>;

[SkipKonduit]
public sealed class SkippedByHandlerClassHandler : IRequestHandler<SkippedByHandlerClass, string>
{
    public Task<string> Handle(SkippedByHandlerClass request, CancellationToken cancellationToken) =>
        Task.FromResult($"quiet {request.Id}");
}

public sealed record SkippedByHandleMethod(int Id) : IRequest<string>;

public sealed class SkippedByHandleMethodHandler : IRequestHandler<SkippedByHandleMethod, string>
{
    [SkipKonduit]
    public Task<string> Handle(SkippedByHandleMethod request, CancellationToken cancellationToken) =>
        Task.FromResult($"quiet {request.Id}");
}

[SkipKonduit]
public sealed record SkippedByMessage(int Id) : IRequest<string>;

public sealed class SkippedByMessageHandler : IRequestHandler<SkippedByMessage, string>
{
    public Task<string> Handle(SkippedByMessage request, CancellationToken cancellationToken) =>
        Task.FromResult($"quiet {request.Id}");
}

public sealed class Recorder
{
    public List<string> Calls { get; } = [];
}
