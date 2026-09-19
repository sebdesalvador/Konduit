using Konduit;

namespace Konduit.Tests.Fakes;

internal sealed class RecordingMiddleware(KonduitDelegate next, string label, List<string> log)
    : IKonduitMiddleware
{
    public async ValueTask InvokeAsync(KonduitContext context)
    {
        log.Add($"{label}:before");
        await next(context);
        log.Add($"{label}:after");
    }
}

internal sealed class AsyncRecordingMiddleware(KonduitDelegate next, string label, List<string> log)
    : IKonduitMiddleware
{
    public async ValueTask InvokeAsync(KonduitContext context)
    {
        await Task.Yield();
        log.Add($"{label}:before");
        await next(context);
        await Task.Yield();
        log.Add($"{label}:after");
    }
}

internal sealed class ResultRewritingMiddleware(KonduitDelegate next) : IKonduitMiddleware
{
    public async ValueTask InvokeAsync(KonduitContext context)
    {
        await next(context);
        context.Result = ((string)context.Result!).ToUpperInvariant();
    }
}

internal sealed class ArgumentRewritingMiddleware(KonduitDelegate next, string replacement)
    : IKonduitMiddleware
{
    public ValueTask InvokeAsync(KonduitContext context)
    {
        context.Arguments[0] = replacement;
        return next(context);
    }
}

internal sealed class ShortCircuitMiddleware(object result) : IKonduitMiddleware
{
    public ValueTask InvokeAsync(KonduitContext context)
    {
        context.Result = result;
        return default;
    }
}

internal sealed class CatchingMiddleware(KonduitDelegate next, Action<Exception> onError)
    : IKonduitMiddleware
{
    public async ValueTask InvokeAsync(KonduitContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            onError(ex);
            throw;
        }
    }
}

internal sealed class ItemWritingMiddleware(KonduitDelegate next, string key, object value)
    : IKonduitMiddleware
{
    public ValueTask InvokeAsync(KonduitContext context)
    {
        context.Items[key] = value;
        return next(context);
    }
}

internal sealed class ItemReadingMiddleware(KonduitDelegate next, string key, Action<object?> onRead)
    : IKonduitMiddleware
{
    public ValueTask InvokeAsync(KonduitContext context)
    {
        onRead(context.Items.TryGetValue(key, out var value) ? value : null);
        return next(context);
    }
}
