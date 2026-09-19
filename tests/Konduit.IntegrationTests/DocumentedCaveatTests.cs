using Konduit;
using Microsoft.Extensions.DependencyInjection;

namespace Konduit.IntegrationTests;

/// <summary>
/// Pins the behaviours the README calls out as caveats, so they stay true or fail loudly.
/// </summary>
public sealed class DocumentedCaveatTests
{
    private static IFeed BuildFeed<TMiddleware>()
        where TMiddleware : class, IKonduitMiddleware =>
        new ServiceCollection()
            .AddSingleton<Trace>()
            .AddScoped<IFeed, Feed>()
            .WithMiddleware<TMiddleware>()
            .BuildServiceProvider()
            .GetRequiredService<IFeed>();

    [Fact]
    public async Task AsyncEnumerableMethodsCompleteTheirPipelineBeforeEnumerationBegins()
    {
        var provider = new ServiceCollection()
            .AddSingleton<Trace>()
            .AddScoped<IFeed, Feed>()
            .WithMiddleware<TracingMiddleware>()
            .BuildServiceProvider();

        var feed = provider.GetRequiredService<IFeed>();
        var trace = provider.GetRequiredService<Trace>();
        var target = provider.GetRequiredService<Feed>();

        var stream = feed.StreamAsync(3);

        // The pipeline has already unwound, even though nothing has been enumerated yet.
        Assert.Equal(2, trace.Entries.Count);
        Assert.StartsWith("StreamAsync:after=", trace.Entries[1], StringComparison.Ordinal);
        Assert.Empty(target.Yielded);

        var items = new List<int>();
        await foreach (var item in stream)
        {
            items.Add(item);
        }

        Assert.Equal([0, 1, 2], items);
        Assert.Equal([0, 1, 2], target.Yielded);

        // Enumeration added nothing to the trace: middleware never saw it.
        Assert.Equal(2, trace.Entries.Count);
    }

    [Fact]
    public void SynchronousMethodsWithSynchronousMiddlewareCompleteWithoutBlocking()
    {
        var feed = BuildFeed<CompletionAssertingMiddleware>();

        Assert.Equal("item 7", feed.Describe(7));
        Assert.True(CompletionAssertingMiddleware.CompletedSynchronously);
    }

    [Fact]
    public void SynchronousMethodsStillWorkWhenMiddlewareGoesAsync()
    {
        var feed = BuildFeed<YieldingMiddleware>();

        Assert.Equal("item 7", feed.Describe(7));
    }

    [Fact]
    public void ExceptionsFromASynchronousTargetKeepTheirOriginalType()
    {
        var feed = BuildFeed<YieldingMiddleware>();

        var ex = Assert.Throws<InvalidOperationException>(() => feed.Fail());

        Assert.Equal("boom", ex.Message);
    }
}

public interface IFeed
{
    IAsyncEnumerable<int> StreamAsync(int count);

    string Describe(int id);

    void Fail();
}

public sealed class Feed : IFeed
{
    public List<int> Yielded { get; } = [];

    public async IAsyncEnumerable<int> StreamAsync(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await Task.Yield();
            Yielded.Add(i);
            yield return i;
        }
    }

    public string Describe(int id) => $"item {id}";

    public void Fail() => throw new InvalidOperationException("boom");
}

/// <summary>Records whether the rest of the pipeline finished without ever going asynchronous.</summary>
public sealed class CompletionAssertingMiddleware(KonduitDelegate next) : IKonduitMiddleware
{
    public static bool CompletedSynchronously { get; private set; }

    public ValueTask InvokeAsync(KonduitContext context)
    {
        var pending = next(context);
        CompletedSynchronously = pending.IsCompletedSuccessfully;
        return pending;
    }
}

public sealed class YieldingMiddleware(KonduitDelegate next) : IKonduitMiddleware
{
    public async ValueTask InvokeAsync(KonduitContext context)
    {
        await Task.Yield();
        await next(context);
    }
}
