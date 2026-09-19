using Konduit;
using Microsoft.Extensions.Logging;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Konduit.MediatR.Tests;

public sealed class KonduitMediatRTests
{
    private static ServiceProvider Build(Action<KonduitMediatRBuilder> configure)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<Recorder>()
            .AddScoped<ScopedProbe>();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(KonduitMediatRTests).Assembly));
        configure(services.AddKonduitToMediatRHandlers());

        return services.BuildServiceProvider();
    }

    private static (IMediator Mediator, Recorder Recorder) Arrange(
        Action<KonduitMediatRBuilder>? configure = null)
    {
        var provider = Build(configure ?? (builder => builder.WithMiddleware<TracingMiddleware>()));

        return (provider.GetRequiredService<IMediator>(), provider.GetRequiredService<Recorder>());
    }

    [Fact]
    public async Task RequestHandlersRunInsideThePipeline()
    {
        var (mediator, recorder) = Arrange();

        var result = await mediator.Send(new Describe(7));

        Assert.Equal("order 7", result);
        Assert.Equal(
            ["DescribeHandler:before", "DescribeHandler(7)", "DescribeHandler:after=order 7"],
            recorder.Calls);
    }

    [Fact]
    public async Task VoidRequestHandlersRunInsideThePipelineWithANullResult()
    {
        var (mediator, recorder) = Arrange();

        await mediator.Send(new Cancel(7));

        Assert.Equal(
            ["CancelHandler:before", "CancelHandler(7)", "CancelHandler:after=null"],
            recorder.Calls);
    }

    [Fact]
    public async Task EveryNotificationHandlerIsWrappedSeparately()
    {
        var (mediator, recorder) = Arrange();

        await mediator.Publish(new Placed(7));

        Assert.Equal(2, recorder.Calls.Count(c => c.EndsWith(":before", StringComparison.Ordinal)));
        Assert.Contains("EmailOnPlaced(7)", recorder.Calls);
        Assert.Contains("AuditOnPlaced(7)", recorder.Calls);
        Assert.Contains("EmailOnPlaced:before", recorder.Calls);
        Assert.Contains("AuditOnPlaced:before", recorder.Calls);
    }

    [Fact]
    public void WrappedNotificationHandlersKeepDistinctConcreteTypes()
    {
        // MediatR identifies the subscribers of a notification by their concrete type. If every
        // wrapper shared one type, MediatR would silently run only the first and the rest would
        // stop firing, so this is a regression guard rather than a style preference.
        var provider = Build(builder => builder.WithMiddleware<TracingMiddleware>());

        var handlers = provider.GetServices<INotificationHandler<Placed>>().ToList();

        Assert.Equal(2, handlers.Count);
        Assert.Equal(2, handlers.Select(handler => handler.GetType()).Distinct().Count());
    }

    [Fact]
    public async Task StreamHandlersRunThePipelineOnceAroundTheCallNotTheIteration()
    {
        var (mediator, recorder) = Arrange();

        // MediatR defers the handler call itself, so nothing runs until enumeration starts.
        var stream = mediator.CreateStream(new StreamItems(3));
        Assert.Empty(recorder.Calls);

        var items = new List<int>();
        await foreach (var item in stream)
        {
            items.Add(item);
        }

        Assert.Equal([0, 1, 2], items);

        // Once it does start, the pipeline runs once and unwinds before any item is produced:
        // middleware here measures the call, not the iteration.
        Assert.StartsWith("StreamItemsHandler:before", recorder.Calls[0], StringComparison.Ordinal);
        Assert.StartsWith("StreamItemsHandler:after", recorder.Calls[1], StringComparison.Ordinal);
        Assert.Equal(["yield(0)", "yield(1)", "yield(2)"], recorder.Calls.Skip(2));
    }

    [Fact]
    public async Task MiddlewareRunsOutsideInInRegistrationOrder()
    {
        var (mediator, recorder) = Arrange(builder => builder
            .WithMiddleware<OuterMiddleware>()
            .WithMiddleware<InnerMiddleware>());

        await mediator.Send(new Describe(7));

        Assert.Equal(
            ["outer:before", "inner:before", "DescribeHandler(7)", "inner:after", "outer:after"],
            recorder.Calls);
    }

    [Fact]
    public async Task MiddlewareCanShortCircuitTheHandler()
    {
        var (mediator, recorder) = Arrange(builder => builder.WithMiddleware<ShortCircuitMiddleware>());

        var result = await mediator.Send(new Describe(7));

        Assert.Equal("from cache", result);
        Assert.Equal(["short-circuited"], recorder.Calls);
    }

    [Fact]
    public async Task MiddlewareCanRewriteTheRequest()
    {
        var (mediator, recorder) = Arrange(builder => builder.WithMiddleware<RequestRewritingMiddleware>());

        var result = await mediator.Send(new Describe(7));

        Assert.Equal("order 70", result);
        Assert.Equal(["DescribeHandler(70)"], recorder.Calls);
    }

    [Fact]
    public async Task TheContextDescribesTheHandlerBeingCalled()
    {
        var (mediator, _) = Arrange(builder => builder.WithMiddleware<MetadataCapturingMiddleware>());

        await mediator.Send(new Describe(7));

        Assert.Equal("Handle", MetadataCapturingMiddleware.LastMethod!.Name);
        Assert.Equal(typeof(IRequestHandler<Describe, string>), MetadataCapturingMiddleware.LastMethod.DeclaringType);
        Assert.Equal(typeof(Task<string>), MetadataCapturingMiddleware.LastMethod.ReturnType);
        Assert.Equal(["request", "cancellationToken"], MetadataCapturingMiddleware.LastMethod.Parameters.Select(p => p.Name));
        Assert.IsType<DescribeHandler>(MetadataCapturingMiddleware.LastTarget);
    }

    [Fact]
    public async Task TheCancellationTokenReachesTheContext()
    {
        using var cts = new CancellationTokenSource();
        var (mediator, _) = Arrange(builder => builder.WithMiddleware<TokenCapturingMiddleware>());

        await mediator.Send(new Describe(7), cts.Token);

        Assert.Equal(cts.Token, TokenCapturingMiddleware.LastToken);
    }

    [Fact]
    public async Task MiddlewareCanInjectScopedDependencies()
    {
        var provider = Build(builder => builder.WithMiddleware<ProbeMiddleware>());

        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new Describe(7));

        Assert.Equal(["DescribeHandler"], scope.ServiceProvider.GetRequiredService<ScopedProbe>().Seen);
    }

    [Theory]
    [InlineData("class")]
    [InlineData("method")]
    [InlineData("message")]
    public async Task SkipKonduitLeavesAHandlerUnwrapped(string placement)
    {
        var (mediator, recorder) = Arrange();

        var result = placement switch
        {
            "class" => await mediator.Send(new SkippedByHandlerClass(7)),
            "method" => await mediator.Send(new SkippedByHandleMethod(7)),
            _ => await mediator.Send(new SkippedByMessage(7)),
        };

        Assert.Equal("quiet 7", result);
        Assert.Empty(recorder.Calls);
    }

    [Fact]
    public void SkippedHandlersAreNotCounted()
    {
        var services = new ServiceCollection().AddLogging().AddSingleton<Recorder>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(KonduitMediatRTests).Assembly));

        var builder = services.AddKonduitToMediatRHandlers();

        // Eight handlers exist in this assembly; the three skipped ones are left as MediatR registered them.
        Assert.Equal(5, builder.HandlerCount);
    }

    [Fact]
    public async Task CallingItTwiceDoesNotNestPipelines()
    {
        var services = new ServiceCollection().AddLogging().AddSingleton<Recorder>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(KonduitMediatRTests).Assembly));
        services.AddKonduitToMediatRHandlers().WithMiddleware<TracingMiddleware>();

        var second = services.AddKonduitToMediatRHandlers();
        Assert.Equal(0, second.HandlerCount);

        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IMediator>().Send(new Describe(7));

        var recorder = provider.GetRequiredService<Recorder>();
        Assert.Single(recorder.Calls, call => call == "DescribeHandler:before");
    }

    [Fact]
    public void CallingItBeforeAddMediatRExplainsTheOrdering()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddLogging().AddKonduitToMediatRHandlers());

        Assert.Contains("after AddMediatR", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandlerLifetimesArePreserved()
    {
        var services = new ServiceCollection().AddLogging().AddSingleton<Recorder>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(KonduitMediatRTests).Assembly));
        services.AddKonduitToMediatRHandlers().WithMiddleware<TracingMiddleware>();

        var descriptor = services.Single(d => d.ServiceType == typeof(IRequestHandler<Describe, string>));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);

        var provider = services.BuildServiceProvider();
        Assert.NotSame(
            provider.GetRequiredService<IRequestHandler<Describe, string>>(),
            provider.GetRequiredService<IRequestHandler<Describe, string>>());

        await Task.CompletedTask;
    }

    [Fact]
    public async Task KonduitRunsInsideMediatRBehaviours()
    {
        var services = new ServiceCollection().AddLogging().AddSingleton<Recorder>();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(KonduitMediatRTests).Assembly);
            cfg.AddOpenBehavior(typeof(RecordingBehaviour<,>));
        });
        services.AddKonduitToMediatRHandlers().WithMiddleware<OuterMiddleware>();

        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IMediator>().Send(new Describe(7));

        Assert.Equal(
            ["behaviour:before", "outer:before", "DescribeHandler(7)", "outer:after", "behaviour:after"],
            provider.GetRequiredService<Recorder>().Calls);
    }
}

public sealed class TokenCapturingMiddleware(KonduitDelegate next) : IKonduitMiddleware
{
    public static CancellationToken LastToken { get; private set; }

    public ValueTask InvokeAsync(KonduitContext context)
    {
        LastToken = context.CancellationToken;
        return next(context);
    }
}

public sealed class RecordingBehaviour<TRequest, TResponse>(Recorder recorder)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        recorder.Calls.Add("behaviour:before");
        var response = await next(cancellationToken);
        recorder.Calls.Add("behaviour:after");
        return response;
    }
}
