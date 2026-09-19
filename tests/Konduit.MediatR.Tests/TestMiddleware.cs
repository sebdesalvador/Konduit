using Konduit;

namespace Konduit.MediatR.Tests;

public sealed class TracingMiddleware(KonduitDelegate next, Recorder recorder) : IKonduitMiddleware
{
    public async ValueTask InvokeAsync(KonduitContext context)
    {
        recorder.Calls.Add($"{Describe(context)}:before");
        await next(context);
        recorder.Calls.Add($"{Describe(context)}:after={context.Result ?? "null"}");
    }

    private static string Describe(KonduitContext context) => context.Target.GetType().Name;
}

public sealed class OuterMiddleware(KonduitDelegate next, Recorder recorder) : IKonduitMiddleware
{
    public async ValueTask InvokeAsync(KonduitContext context)
    {
        recorder.Calls.Add("outer:before");
        await next(context);
        recorder.Calls.Add("outer:after");
    }
}

public sealed class InnerMiddleware(KonduitDelegate next, Recorder recorder) : IKonduitMiddleware
{
    public async ValueTask InvokeAsync(KonduitContext context)
    {
        recorder.Calls.Add("inner:before");
        await next(context);
        recorder.Calls.Add("inner:after");
    }
}

public sealed class ShortCircuitMiddleware : IKonduitMiddleware
{
    private readonly Recorder _recorder;

    // next is never called — that is the point — but it stays in the signature because
    // Konduit supplies it positionally when activating middleware.
    public ShortCircuitMiddleware(KonduitDelegate next, Recorder recorder)
    {
        _ = next;
        _recorder = recorder;
    }

    public ValueTask InvokeAsync(KonduitContext context)
    {
        _recorder.Calls.Add("short-circuited");
        context.Result = "from cache";
        return default;
    }
}

public sealed class RequestRewritingMiddleware(KonduitDelegate next) : IKonduitMiddleware
{
    public ValueTask InvokeAsync(KonduitContext context)
    {
        if (context.Arguments[0] is Describe describe)
        {
            context.Arguments[0] = describe with { Id = describe.Id * 10 };
        }

        return next(context);
    }
}

public sealed class MetadataCapturingMiddleware(KonduitDelegate next) : IKonduitMiddleware
{
    public static KonduitMethod? LastMethod { get; private set; }

    public static object? LastTarget { get; private set; }

    public ValueTask InvokeAsync(KonduitContext context)
    {
        LastMethod = context.Method;
        LastTarget = context.Target;
        return next(context);
    }
}

public sealed class ScopedProbe
{
    public List<string> Seen { get; } = [];
}

public sealed class ProbeMiddleware(KonduitDelegate next, ScopedProbe probe) : IKonduitMiddleware
{
    public ValueTask InvokeAsync(KonduitContext context)
    {
        probe.Seen.Add(context.Target.GetType().Name);
        return next(context);
    }
}
