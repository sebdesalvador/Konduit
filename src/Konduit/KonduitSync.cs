namespace Konduit;

/// <summary>
/// Bridges the asynchronous pipeline back to synchronous interface methods. Called by generated proxies.
/// </summary>
public static class KonduitSync
{
    /// <summary>
    /// Completes a pipeline invocation on behalf of a synchronous method.
    /// </summary>
    /// <param name="pending">The pipeline invocation to complete.</param>
    /// <remarks>
    /// When every middleware in the chain ran synchronously — the usual case for logging, validation
    /// or metrics — the pipeline has already finished and this returns without blocking at all. It
    /// blocks only when a middleware genuinely went asynchronous inside a synchronous call, which
    /// risks the usual sync-over-async deadlocks; prefer asynchronous interface methods when the
    /// middleware around them does real I/O.
    /// </remarks>
    public static void Wait(ValueTask pending)
    {
        if (pending.IsCompleted)
        {
            pending.GetAwaiter().GetResult();
            return;
        }

        pending.AsTask().GetAwaiter().GetResult();
    }
}
