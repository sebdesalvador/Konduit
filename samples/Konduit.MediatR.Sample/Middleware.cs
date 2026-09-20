using System.Diagnostics;
using Konduit;

namespace Konduit.MediatR.Sample;

/// <summary>Logs every handler call and what it produced.</summary>
public sealed class LoggingMiddleware(KonduitDelegate next) : IKonduitMiddleware
{
    public async ValueTask InvokeAsync(KonduitContext context)
    {
        var handler = context.Target.GetType().Name;
        var started = Stopwatch.GetTimestamp();

        Console.WriteLine($"  → {handler}({context.Arguments[0]})");

        await next(context);

        Console.WriteLine(
            $"  ← {handler} = {context.Result ?? "void"} " +
            $"in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0}ms");
    }
}

/// <summary>Rejects a request before the handler ever runs.</summary>
public sealed class ValidationMiddleware(KonduitDelegate next) : IKonduitMiddleware
{
    public ValueTask InvokeAsync(KonduitContext context)
    {
        if (context.Arguments[0] is GetOrder { Id: <= 0 } bad)
        {
            throw new ArgumentOutOfRangeException(nameof(context), $"order id {bad.Id} is not valid");
        }

        return next(context);
    }
}
