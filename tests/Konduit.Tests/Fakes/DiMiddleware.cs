using Konduit;

namespace Konduit.Tests.Fakes;

internal sealed class PassThroughMiddleware(KonduitDelegate next) : IKonduitMiddleware
{
    public ValueTask InvokeAsync(KonduitContext context) => next(context);
}

internal sealed class LoggingMiddleware(KonduitDelegate next, List<string> log) : IKonduitMiddleware
{
    public async ValueTask InvokeAsync(KonduitContext context)
    {
        log.Add($"{context.Method.Name}:before");
        await next(context);
        log.Add($"{context.Method.Name}:after");
    }
}

internal sealed class OuterMiddleware(KonduitDelegate next, List<string> log) : IKonduitMiddleware
{
    public async ValueTask InvokeAsync(KonduitContext context)
    {
        log.Add("outer:before");
        await next(context);
        log.Add("outer:after");
    }
}

internal sealed class InnerMiddleware(KonduitDelegate next, List<string> log) : IKonduitMiddleware
{
    public async ValueTask InvokeAsync(KonduitContext context)
    {
        log.Add("inner:before");
        await next(context);
        log.Add("inner:after");
    }
}

internal sealed class ResultCapturingMiddleware(KonduitDelegate next) : IKonduitMiddleware
{
    public static object? LastResult { get; private set; }

    public async ValueTask InvokeAsync(KonduitContext context)
    {
        await next(context);
        LastResult = context.Result;
    }
}

internal sealed class ScopedProbe
{
    public List<string> Calls { get; } = [];
}

internal sealed class ProbeRecordingMiddleware(KonduitDelegate next, ScopedProbe probe) : IKonduitMiddleware
{
    public ValueTask InvokeAsync(KonduitContext context)
    {
        probe.Calls.Add(context.Method.Name);
        return next(context);
    }
}
