using Konduit;
using Konduit.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace Konduit.Tests.DependencyInjection;

public sealed class WithMiddlewareValidationTests
{
    [Fact]
    public void WithMiddleware_OnAnEmptyCollection_ExplainsThatNothingWasRegistered()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().WithMiddleware<PassThroughMiddleware>());

        Assert.Contains("must follow a service registration", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WithMiddleware_OnANonInterfaceService_ExplainsTheLimitation()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new ServiceCollection()
            .AddScoped<Greeter>()
            .WithMiddleware<PassThroughMiddleware>());

        Assert.Contains("interface", ex.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(Greeter), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WithMiddleware_OnAnOpenGeneric_ExplainsTheLimitation()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new ServiceCollection()
            .AddScoped(typeof(IStore<>), typeof(Store<>))
            .WithMiddleware<PassThroughMiddleware>());

        Assert.Contains("open generic", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WithMiddleware_WithoutAGeneratedProxy_PointsAtTheGenerator()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new ServiceCollection()
            .AddScoped<IUnproxied, Unproxied>()
            .WithMiddleware<PassThroughMiddleware>());

        Assert.Contains(nameof(IUnproxied), ex.Message, StringComparison.Ordinal);
        Assert.Contains("source generator", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WithMiddleware_OnANullCollection_Throws() =>
        Assert.Throws<ArgumentNullException>(
            () => KonduitServiceCollectionExtensions.WithMiddleware<PassThroughMiddleware>(null!));

    private interface IStore<T>;

    private sealed class Store<T> : IStore<T>;

    private interface IUnproxied;

    private sealed class Unproxied : IUnproxied;
}
