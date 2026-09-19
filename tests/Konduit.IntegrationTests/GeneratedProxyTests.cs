using Konduit;
using Microsoft.Extensions.DependencyInjection;

namespace Konduit.IntegrationTests;

public sealed class GeneratedProxyTests
{
    private static (IOrderService Service, OrderService Target, Trace Trace) Build()
    {
        var provider = new ServiceCollection()
            .AddSingleton<Trace>()
            .AddScoped<IOrderService, OrderService>()
            .WithMiddleware<TracingMiddleware>()
            .BuildServiceProvider();

        return (
            provider.GetRequiredService<IOrderService>(),
            provider.GetRequiredService<OrderService>(),
            provider.GetRequiredService<Trace>());
    }

    [Fact]
    public void TheResolvedServiceIsAGeneratedProxy()
    {
        var (service, target, _) = Build();

        Assert.IsAssignableFrom<IKonduitProxy>(service);
        Assert.Same(target, ((IKonduitProxy)service).KonduitTarget);
    }

    [Fact]
    public void SynchronousValueReturningMethodsAreIntercepted()
    {
        var (service, _, trace) = Build();

        Assert.Equal("order 7", service.Describe(7));
        Assert.Equal(["Describe:before", "Describe:after=order 7"], trace.Entries);
    }

    [Fact]
    public void VoidMethodsAreIntercepted()
    {
        var (service, target, trace) = Build();

        service.Cancel(7);

        Assert.Equal(["Cancel(7)"], target.Calls);
        Assert.Equal(["Cancel:before", "Cancel:after=null"], trace.Entries);
    }

    [Fact]
    public async Task TaskReturningMethodsAreIntercepted()
    {
        var (service, target, trace) = Build();

        await service.SubmitAsync(7, CancellationToken.None);

        Assert.Equal(["SubmitAsync(7)"], target.Calls);
        Assert.Equal(["SubmitAsync:before", "SubmitAsync:after=null"], trace.Entries);
    }

    [Fact]
    public async Task MiddlewareSeesTheAwaitedValueOfATaskOfT()
    {
        var (service, _, trace) = Build();

        Assert.Equal("confirmed 7", await service.ConfirmAsync(7, CancellationToken.None));
        Assert.Equal(["ConfirmAsync:before", "ConfirmAsync:after=confirmed 7"], trace.Entries);
    }

    [Fact]
    public async Task ValueTaskReturningMethodsAreIntercepted()
    {
        var (service, target, trace) = Build();

        await service.ArchiveAsync(7);

        Assert.Equal(["ArchiveAsync(7)"], target.Calls);
        Assert.Equal(["ArchiveAsync:before", "ArchiveAsync:after=null"], trace.Entries);
    }

    [Fact]
    public async Task MiddlewareSeesTheAwaitedValueOfAValueTaskOfT()
    {
        var (service, _, trace) = Build();

        Assert.Equal(1, await service.CountAsync());
        Assert.Equal(["CountAsync:before", "CountAsync:after=1"], trace.Entries);
    }

    [Fact]
    public void NullableReferenceReturnsSurviveTheRoundTrip()
    {
        var (service, _, _) = Build();

        Assert.Null(service.FindReference(0));
        Assert.Equal("ref-7", service.FindReference(7));
        Assert.Null(service.FindTags(0));
        Assert.Equal(["a", "b"], service.FindTags(7));
    }

    [Fact]
    public void GenericMethodsAreIntercepted()
    {
        var (service, _, trace) = Build();

        var result = service.Read<OrderDraft>("draft");

        Assert.NotNull(result);
        Assert.Equal(["Read:before", $"Read:after={result}"], trace.Entries);
    }

    [Fact]
    public void OverloadsAreDistinguished()
    {
        var (service, _, trace) = Build();

        Assert.Equal("order 7", service.Describe(7));
        Assert.Equal("order 7 rush", service.Describe(7, "rush"));
        Assert.Equal(
            ["Describe:before", "Describe:after=order 7", "Describe:before", "Describe:after=order 7 rush"],
            trace.Entries);
    }

    [Fact]
    public void SkipKonduitMethodsBypassThePipeline()
    {
        var (service, target, trace) = Build();

        Assert.Equal("order 7", service.DescribeQuietly(7));
        Assert.Equal(["DescribeQuietly(7)"], target.Calls);
        Assert.Empty(trace.Entries);
    }

    [Fact]
    public void InheritedPropertiesForwardToTheTarget()
    {
        var (service, target, trace) = Build();

        service.LastAction = "cancelled";

        Assert.Equal("cancelled", target.LastAction);
        Assert.Equal("cancelled", service.LastAction);
        Assert.Empty(trace.Entries);
    }

    [Fact]
    public void IndexersForwardToTheTarget()
    {
        var (service, _, _) = Build();

        Assert.Equal("item 3", service[3]);
    }

    [Fact]
    public void InheritedEventsForwardToTheTarget()
    {
        var (service, target, _) = Build();
        var raised = 0;

        service.Audited += (_, _) => raised++;
        target.RaiseAudited();

        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task TheCancellationTokenParameterReachesTheContext()
    {
        using var cts = new CancellationTokenSource();
        var provider = new ServiceCollection()
            .AddSingleton<Trace>()
            .AddScoped<IOrderService, OrderService>()
            .WithMiddleware<TokenCapturingMiddleware>()
            .BuildServiceProvider();

        await provider.GetRequiredService<IOrderService>().SubmitAsync(7, cts.Token);

        Assert.Equal(cts.Token, TokenCapturingMiddleware.LastToken);
    }

    [Fact]
    public void MethodsWithoutACancellationTokenLeaveItAtNone()
    {
        var provider = new ServiceCollection()
            .AddScoped<IOrderService, OrderService>()
            .WithMiddleware<TokenCapturingMiddleware>()
            .BuildServiceProvider();

        provider.GetRequiredService<IOrderService>().Describe(7);

        Assert.Equal(CancellationToken.None, TokenCapturingMiddleware.LastToken);
    }

    [Fact]
    public void MethodMetadataIsAvailableToMiddleware()
    {
        var provider = new ServiceCollection()
            .AddScoped<IOrderService, OrderService>()
            .WithMiddleware<MetadataCapturingMiddleware>()
            .BuildServiceProvider();

        provider.GetRequiredService<IOrderService>().Describe(7, "rush");

        Assert.Equal("Describe", MetadataCapturingMiddleware.LastMethod!.Name);
        Assert.Equal(typeof(IOrderService), MetadataCapturingMiddleware.LastMethod.DeclaringType);
        Assert.Equal(typeof(string), MetadataCapturingMiddleware.LastMethod.ReturnType);
        Assert.Equal(["id", "suffix"], MetadataCapturingMiddleware.LastMethod.Parameters.Select(p => p.Name));
        Assert.Equal("Describe", MetadataCapturingMiddleware.LastMethod.MethodInfo.Name);
    }
}

public sealed class OrderDraft;

public sealed class TokenCapturingMiddleware(KonduitDelegate next) : IKonduitMiddleware
{
    public static CancellationToken LastToken { get; private set; }

    public ValueTask InvokeAsync(KonduitContext context)
    {
        LastToken = context.CancellationToken;
        return next(context);
    }
}

public sealed class MetadataCapturingMiddleware(KonduitDelegate next) : IKonduitMiddleware
{
    public static KonduitMethod? LastMethod { get; private set; }

    public ValueTask InvokeAsync(KonduitContext context)
    {
        LastMethod = context.Method;
        return next(context);
    }
}
