namespace Konduit;

/// <summary>
/// Composes middleware into the single <see cref="KonduitDelegate"/> a generated proxy calls.
/// </summary>
/// <remarks>
/// Middleware runs outside-in in registration order: the first <see cref="Use"/> is the outermost
/// wrapper, exactly as with <c>app.Use(...)</c> in ASP.NET Core. The built delegate is method-agnostic,
/// so one pipeline serves every method on the service; the target call itself travels on the context.
/// </remarks>
public sealed class KonduitPipelineBuilder
{
    private static readonly KonduitDelegate TargetInvoker = static context => context.InvokeTargetAsync();

    private readonly List<Func<KonduitDelegate, IKonduitMiddleware>> _factories = [];

    /// <summary>
    /// Appends a middleware to the pipeline, inside everything added before it.
    /// </summary>
    /// <param name="factory">Creates the middleware, given the next step in the pipeline.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    public KonduitPipelineBuilder Use(Func<KonduitDelegate, IKonduitMiddleware> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factories.Add(factory);
        return this;
    }

    /// <summary>
    /// Builds the pipeline.
    /// </summary>
    /// <returns>
    /// A delegate that runs every registered middleware and then the target method. With no
    /// middleware registered, it invokes the target directly.
    /// </returns>
    /// <exception cref="InvalidOperationException">A middleware factory returned <see langword="null"/>.</exception>
    public KonduitDelegate Build()
    {
        var next = TargetInvoker;

        for (var i = _factories.Count - 1; i >= 0; i--)
        {
            var middleware = _factories[i](next)
                ?? throw new InvalidOperationException(
                    "A Konduit middleware factory returned null. Every factory must return a middleware instance.");

            next = middleware.InvokeAsync;
        }

        return next;
    }
}
