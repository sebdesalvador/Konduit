using Konduit;

namespace Konduit.IntegrationTests;

public sealed class TracingMiddleware(KonduitDelegate next, Trace trace) : IKonduitMiddleware
{
    public async ValueTask InvokeAsync(KonduitContext context)
    {
        trace.Entries.Add($"{context.Method.Name}:before");
        await next(context);
        trace.Entries.Add($"{context.Method.Name}:after={context.Result ?? "null"}");
    }
}

public sealed class Trace
{
    public List<string> Entries { get; } = [];
}
