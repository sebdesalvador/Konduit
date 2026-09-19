using Konduit;
using Konduit.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace Konduit.Tests.DependencyInjection;

public sealed class WithMiddlewareTests
{
    [Fact]
    public void Resolving_TheService_ReturnsTheGeneratedProxy()
    {
        var provider = new ServiceCollection()
            .AddScoped<IGreeter, Greeter>()
            .WithMiddleware<PassThroughMiddleware>()
            .BuildServiceProvider();

        var greeter = provider.GetRequiredService<IGreeter>();

        Assert.IsType<GreeterKonduitProxy>(greeter);
    }

    [Fact]
    public void Resolving_WithoutWithMiddleware_ReturnsThePlainImplementation()
    {
        var provider = new ServiceCollection()
            .AddScoped<IGreeter, Greeter>()
            .BuildServiceProvider();

        Assert.IsType<Greeter>(provider.GetRequiredService<IGreeter>());
    }

    [Fact]
    public void Middleware_RunsAroundASynchronousCall()
    {
        var log = new List<string>();
        var provider = new ServiceCollection()
            .AddSingleton(log)
            .AddScoped<IGreeter, Greeter>()
            .WithMiddleware<LoggingMiddleware>()
            .BuildServiceProvider();

        var result = provider.GetRequiredService<IGreeter>().Greet("Ada");

        Assert.Equal("Hello, Ada", result);
        Assert.Equal(["Greet:before", "Greet:after"], log);
    }

    [Fact]
    public async Task Middleware_SeesTheAwaitedResultOfAnAsynchronousCall()
    {
        var provider = new ServiceCollection()
            .AddScoped<IGreeter, Greeter>()
            .WithMiddleware<ResultCapturingMiddleware>()
            .BuildServiceProvider();

        var result = await provider.GetRequiredService<IGreeter>().GreetAsync("Ada", CancellationToken.None);

        Assert.Equal("Hello, Ada", result);
        Assert.Equal("Hello, Ada", ResultCapturingMiddleware.LastResult);
    }

    [Fact]
    public void Middleware_RunsAroundAVoidReturningCall()
    {
        var log = new List<string>();
        var provider = new ServiceCollection()
            .AddSingleton(log)
            .AddScoped<IGreeter, Greeter>()
            .WithMiddleware<LoggingMiddleware>()
            .BuildServiceProvider();

        provider.GetRequiredService<IGreeter>().Record("Ada");

        Assert.Equal(["Record:before", "Record:after"], log);
    }

    [Fact]
    public void SkipKonduit_MethodsBypassThePipelineEntirely()
    {
        var log = new List<string>();
        var provider = new ServiceCollection()
            .AddSingleton(log)
            .AddScoped<IGreeter, Greeter>()
            .WithMiddleware<LoggingMiddleware>()
            .BuildServiceProvider();

        var result = provider.GetRequiredService<IGreeter>().GreetDirectly("Ada");

        Assert.Equal("Hello, Ada", result);
        Assert.Empty(log);
    }

    [Fact]
    public void Middleware_RunsOutsideInInRegistrationOrder()
    {
        var log = new List<string>();
        var provider = new ServiceCollection()
            .AddSingleton(log)
            .AddScoped<IGreeter, Greeter>()
            .WithMiddleware<OuterMiddleware>()
            .WithMiddleware<InnerMiddleware>()
            .BuildServiceProvider();

        provider.GetRequiredService<IGreeter>().Greet("Ada");

        Assert.Equal(["outer:before", "inner:before", "inner:after", "outer:after"], log);
    }

    [Fact]
    public void Scoped_Registrations_KeepScopedLifetime()
    {
        var provider = new ServiceCollection()
            .AddScoped<IGreeter, Greeter>()
            .WithMiddleware<PassThroughMiddleware>()
            .BuildServiceProvider();

        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        Assert.Same(
            first.ServiceProvider.GetRequiredService<IGreeter>(),
            first.ServiceProvider.GetRequiredService<IGreeter>());
        Assert.NotSame(
            first.ServiceProvider.GetRequiredService<IGreeter>(),
            second.ServiceProvider.GetRequiredService<IGreeter>());
    }

    [Fact]
    public void Singleton_Registrations_KeepSingletonLifetime()
    {
        var provider = new ServiceCollection()
            .AddSingleton<IGreeter, Greeter>()
            .WithMiddleware<PassThroughMiddleware>()
            .BuildServiceProvider();

        using var scope = provider.CreateScope();

        Assert.Same(
            provider.GetRequiredService<IGreeter>(),
            scope.ServiceProvider.GetRequiredService<IGreeter>());
    }

    [Fact]
    public void Transient_Registrations_KeepTransientLifetime()
    {
        var provider = new ServiceCollection()
            .AddTransient<IGreeter, Greeter>()
            .WithMiddleware<PassThroughMiddleware>()
            .BuildServiceProvider();

        Assert.NotSame(
            provider.GetRequiredService<IGreeter>(),
            provider.GetRequiredService<IGreeter>());
    }

    [Fact]
    public void Middleware_CanInjectScopedDependencies()
    {
        var provider = new ServiceCollection()
            .AddScoped<ScopedProbe>()
            .AddScoped<IGreeter, Greeter>()
            .WithMiddleware<ProbeRecordingMiddleware>()
            .BuildServiceProvider();

        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IGreeter>().Greet("Ada");

        Assert.Equal(["Greet"], scope.ServiceProvider.GetRequiredService<ScopedProbe>().Calls);
    }

    [Fact]
    public void ImplementationType_RemainsResolvableDirectly()
    {
        var provider = new ServiceCollection()
            .AddScoped<IGreeter, Greeter>()
            .WithMiddleware<PassThroughMiddleware>()
            .BuildServiceProvider();

        using var scope = provider.CreateScope();

        Assert.Same(
            ((IKonduitProxy)scope.ServiceProvider.GetRequiredService<IGreeter>()).KonduitTarget,
            scope.ServiceProvider.GetRequiredService<Greeter>());
    }

    [Fact]
    public void ImplementationFactory_Registrations_AreWrappedToo()
    {
        var instance = new Greeter();
        var provider = new ServiceCollection()
            .AddScoped<IGreeter>(_ => instance)
            .WithMiddleware<PassThroughMiddleware>()
            .BuildServiceProvider();

        var greeter = provider.GetRequiredService<IGreeter>();

        Assert.IsType<GreeterKonduitProxy>(greeter);
        Assert.Same(instance, ((IKonduitProxy)greeter).KonduitTarget);
    }

    [Fact]
    public void ImplementationInstance_Registrations_AreWrappedToo()
    {
        var instance = new Greeter();
        var provider = new ServiceCollection()
            .AddSingleton<IGreeter>(instance)
            .WithMiddleware<PassThroughMiddleware>()
            .BuildServiceProvider();

        var greeter = provider.GetRequiredService<IGreeter>();

        Assert.IsType<GreeterKonduitProxy>(greeter);
        Assert.Same(instance, ((IKonduitProxy)greeter).KonduitTarget);
    }
}
