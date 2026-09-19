namespace Konduit;

/// <summary>
/// State for a single intercepted call, shared by every middleware in the pipeline.
/// </summary>
/// <remarks>
/// Like ASP.NET Core's <c>HttpContext</c>, this object is deliberately mutable: rewriting
/// <see cref="Arguments"/> on the way in and <see cref="Result"/> on the way out is how middleware
/// does its job. One instance is created per intercepted call and must not outlive it.
/// </remarks>
public sealed class KonduitContext
{
    private readonly KonduitDelegate _terminal;
    private Dictionary<string, object?>? _items;

    /// <summary>
    /// Initializes a new <see cref="KonduitContext"/>. Called by generated proxies.
    /// </summary>
    /// <param name="target">The service implementation the call ultimately reaches.</param>
    /// <param name="method">The interface method being called.</param>
    /// <param name="arguments">The call arguments, in declaration order.</param>
    /// <param name="services">The provider that resolved the service being called.</param>
    /// <param name="terminal">Invokes the target method and stores its value in <see cref="Result"/>.</param>
    public KonduitContext(
        object target,
        KonduitMethod method,
        object?[] arguments,
        IServiceProvider services,
        KonduitDelegate terminal)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(terminal);

        Target = target;
        Method = method;
        Arguments = arguments;
        Services = services;
        _terminal = terminal;
    }

    /// <summary>Gets the service implementation the call ultimately reaches.</summary>
    public object Target { get; }

    /// <summary>Gets the interface method being called.</summary>
    public KonduitMethod Method { get; }

    /// <summary>
    /// Gets the call arguments, in declaration order.
    /// </summary>
    /// <remarks>
    /// Writing to this array before calling <c>next</c> changes what the target method receives.
    /// </remarks>
    public object?[] Arguments { get; }

    /// <summary>Gets the provider that resolved the service being called.</summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Gets or sets the value the call returns.
    /// </summary>
    /// <remarks>
    /// For an asynchronous method this is the awaited value, not the task, so middleware that reads
    /// it after <c>next</c> sees the finished work. It is <see langword="null"/> for methods
    /// returning <see langword="void"/>, <see cref="Task"/> or <see cref="ValueTask"/>.
    /// </remarks>
    public object? Result { get; set; }

    /// <summary>
    /// Gets the cancellation token for this call.
    /// </summary>
    /// <remarks>
    /// Generated proxies set this from the method's own <see cref="System.Threading.CancellationToken"/>
    /// parameter when it has one; otherwise it is <see cref="CancellationToken.None"/>.
    /// </remarks>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Gets a bag for passing state between middleware within this call.
    /// </summary>
    /// <remarks>Allocated on first access, so calls that never use it pay nothing.</remarks>
    public IDictionary<string, object?> Items => _items ??= new Dictionary<string, object?>(StringComparer.Ordinal);

    internal ValueTask InvokeTargetAsync() => _terminal(this);
}
