using System.Diagnostics;
using Konduit;

namespace Konduit.Http.Sample;

/// <summary>
/// Logs the interface call, not the HTTP exchange: the method name and the value it returned.
/// </summary>
public sealed class LoggingMiddleware(KonduitDelegate next) : IKonduitMiddleware
{
    public async ValueTask InvokeAsync(KonduitContext context)
    {
        var started = Stopwatch.GetTimestamp();
        Console.WriteLine($"  → {context.Method.Name}({string.Join(", ", context.Arguments[..^1])})");

        await next(context);

        Console.WriteLine(
            $"  ← {context.Method.Name} = \"{context.Result}\" " +
            $"in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0}ms");
    }
}

/// <summary>Retries the call, not the request, so the whole method runs again.</summary>
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
            catch (HttpRequestException ex) when (attempt < MaxAttempts)
            {
                Console.WriteLine($"    ! attempt {attempt} failed ({ex.Message}), retrying");
            }
        }
    }
}

/// <summary>
/// Short-circuits a repeat call, so no HTTP request is made at all — something a
/// <see cref="DelegatingHandler"/> could only do after the request had been built.
/// </summary>
public sealed class CachingMiddleware(KonduitDelegate next) : IKonduitMiddleware
{
    private readonly Dictionary<string, object?> _cache = new(StringComparer.Ordinal);

    public async ValueTask InvokeAsync(KonduitContext context)
    {
        var key = $"{context.Method.Name}({string.Join(",", context.Arguments[..^1])})";

        if (_cache.TryGetValue(key, out var cached))
        {
            Console.WriteLine($"    * cache hit for {key} — no HTTP call");
            context.Result = cached;
            return;
        }

        await next(context);
        _cache[key] = context.Result;
    }
}
