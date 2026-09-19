namespace Konduit;

/// <summary>
/// Wraps every intercepted call to a service, running code before and after the target method.
/// </summary>
/// <remarks>
/// Implementations take the next step in the pipeline as a <see cref="KonduitDelegate"/> constructor
/// parameter; every other constructor parameter is resolved from the service provider that resolved
/// the service being wrapped.
/// </remarks>
/// <example>
/// <code>
/// public sealed class TimingMiddleware(KonduitDelegate next, ILogger&lt;TimingMiddleware&gt; logger)
///     : IKonduitMiddleware
/// {
///     public async ValueTask InvokeAsync(KonduitContext context)
///     {
///         var started = Stopwatch.GetTimestamp();
///         await next(context);
///         logger.LogInformation("{Method} took {Elapsed}", context.Method.Name, Stopwatch.GetElapsedTime(started));
///     }
/// }
/// </code>
/// </example>
public interface IKonduitMiddleware
{
    /// <summary>
    /// Runs this middleware for a single intercepted call.
    /// </summary>
    /// <param name="context">State for the call currently in flight.</param>
    /// <returns>A task that completes once this middleware, and everything it invoked, have run.</returns>
    ValueTask InvokeAsync(KonduitContext context);
}
