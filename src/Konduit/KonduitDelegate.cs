namespace Konduit;

/// <summary>
/// A single step in a Konduit pipeline.
/// </summary>
/// <remarks>
/// This is the service-call analogue of ASP.NET Core's <c>RequestDelegate</c>. Middleware receives
/// one of these as its <c>next</c> constructor parameter and calls it to continue the pipeline.
/// </remarks>
/// <param name="context">State for the call currently in flight.</param>
/// <returns>
/// A task that completes once the rest of the pipeline, and the target method itself, have run.
/// </returns>
public delegate ValueTask KonduitDelegate(KonduitContext context);
