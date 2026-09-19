using System.Diagnostics;
using Konduit;

namespace Konduit.Sample;

/// <summary>Logs every call, and the value it produced, on the way back out.</summary>
public sealed class LoggingMiddleware(KonduitDelegate next) : IKonduitMiddleware
{
    public async ValueTask InvokeAsync(KonduitContext context)
    {
        var started = Stopwatch.GetTimestamp();
        Console.WriteLine($"  → {context.Method.Name}({string.Join(", ", context.Arguments)})");

        await next(context);

        Console.WriteLine(
            $"  ← {context.Method.Name} = {context.Result ?? "void"} " +
            $"in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0}ms");
    }
}

/// <summary>Retries a failed call, showing that middleware can call <c>next</c> more than once.</summary>
public sealed class RetryMiddleware(KonduitDelegate next) : IKonduitMiddleware
{
    private const int MaxAttempts = 3;

    public async ValueTask InvokeAsync(KonduitContext context)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await next(context);
                return;
            }
            catch (TimeoutException ex) when (attempt < MaxAttempts)
            {
                Console.WriteLine($"    ! attempt {attempt} failed ({ex.Message}), retrying");
            }
        }
    }
}

/// <summary>Short-circuits repeated calls, so the target is never reached on a hit.</summary>
public sealed class CachingMiddleware(KonduitDelegate next) : IKonduitMiddleware
{
    private readonly Dictionary<string, object?> _cache = new(StringComparer.Ordinal);

    public async ValueTask InvokeAsync(KonduitContext context)
    {
        var key = $"{context.Method.Name}({string.Join(",", context.Arguments)})";

        if (_cache.TryGetValue(key, out var cached))
        {
            Console.WriteLine($"    * cache hit for {key}");
            context.Result = cached;
            return;
        }

        await next(context);
        _cache[key] = context.Result;
    }
}
