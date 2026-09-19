using Konduit;
using Konduit.Tests.Fakes;

namespace Konduit.Tests.Pipeline;

public sealed class KonduitPipelineBuilderTests
{
    [Fact]
    public async Task Build_WithNoMiddleware_InvokesTargetDirectly()
    {
        var greeter = new Greeter();
        var pipeline = new KonduitPipelineBuilder().Build();
        var context = TestContext.For(greeter, "Ada");

        await pipeline(context);

        Assert.Equal("Hello, Ada", context.Result);
        Assert.Equal(["Ada"], greeter.SeenNames);
    }

    [Fact]
    public async Task Build_WithSingleMiddleware_RunsBeforeAndAfterTarget()
    {
        var log = new List<string>();
        var pipeline = new KonduitPipelineBuilder()
            .Use(next => new RecordingMiddleware(next, "a", log))
            .Build();

        await pipeline(TestContext.For(new Greeter(), "Ada"));

        Assert.Equal(["a:before", "a:after"], log);
    }

    [Fact]
    public async Task Build_WithMultipleMiddleware_RunsOutsideInInRegistrationOrder()
    {
        var log = new List<string>();
        var pipeline = new KonduitPipelineBuilder()
            .Use(next => new RecordingMiddleware(next, "outer", log))
            .Use(next => new RecordingMiddleware(next, "inner", log))
            .Build();

        await pipeline(TestContext.For(new Greeter(), "Ada"));

        Assert.Equal(["outer:before", "inner:before", "inner:after", "outer:after"], log);
    }

    [Fact]
    public async Task Middleware_CanReplaceResult()
    {
        var pipeline = new KonduitPipelineBuilder()
            .Use(next => new ResultRewritingMiddleware(next))
            .Build();
        var context = TestContext.For(new Greeter(), "Ada");

        await pipeline(context);

        Assert.Equal("HELLO, ADA", context.Result);
    }

    [Fact]
    public async Task Middleware_CanMutateArguments_AndTargetSeesTheChange()
    {
        var greeter = new Greeter();
        var pipeline = new KonduitPipelineBuilder()
            .Use(next => new ArgumentRewritingMiddleware(next, "Grace"))
            .Build();

        await pipeline(TestContext.For(greeter, "Ada"));

        Assert.Equal(["Grace"], greeter.SeenNames);
    }

    [Fact]
    public async Task Middleware_CanShortCircuit_AndTargetIsNeverInvoked()
    {
        var greeter = new Greeter();
        var pipeline = new KonduitPipelineBuilder()
            .Use(_ => new ShortCircuitMiddleware("cached"))
            .Build();
        var context = TestContext.For(greeter, "Ada");

        await pipeline(context);

        Assert.Equal("cached", context.Result);
        Assert.Empty(greeter.SeenNames);
    }

    [Fact]
    public async Task Middleware_CanObserveExceptionsThrownByTheTarget()
    {
        Exception? observed = null;
        var pipeline = new KonduitPipelineBuilder()
            .Use(next => new CatchingMiddleware(next, ex => observed = ex))
            .Build();
        var context = TestContext.For(
            new Greeter(),
            "Ada",
            terminal: _ => throw new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await pipeline(context));

        Assert.NotNull(observed);
        Assert.Equal("boom", observed!.Message);
    }

    [Fact]
    public async Task Items_AreSharedBetweenMiddleware()
    {
        object? seen = null;
        var pipeline = new KonduitPipelineBuilder()
            .Use(next => new ItemWritingMiddleware(next, "key", 42))
            .Use(next => new ItemReadingMiddleware(next, "key", value => seen = value))
            .Build();

        await pipeline(TestContext.For(new Greeter(), "Ada"));

        Assert.Equal(42, seen);
    }

    [Fact]
    public void Build_WhenEveryMiddlewareIsSynchronous_CompletesWithoutBlocking()
    {
        var log = new List<string>();
        var pipeline = new KonduitPipelineBuilder()
            .Use(next => new RecordingMiddleware(next, "a", log))
            .Use(next => new RecordingMiddleware(next, "b", log))
            .Build();

        var pending = pipeline(TestContext.For(new Greeter(), "Ada"));

        Assert.True(pending.IsCompletedSuccessfully);
    }

    [Fact]
    public void Use_WithNullFactory_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new KonduitPipelineBuilder().Use(null!));

    [Fact]
    public async Task Build_WithAsynchronousMiddleware_StillUnwindsInOrder()
    {
        var log = new List<string>();
        var pipeline = new KonduitPipelineBuilder()
            .Use(next => new AsyncRecordingMiddleware(next, "outer", log))
            .Use(next => new AsyncRecordingMiddleware(next, "inner", log))
            .Build();

        await pipeline(TestContext.For(new Greeter(), "Ada"));

        Assert.Equal(["outer:before", "inner:before", "inner:after", "outer:after"], log);
    }
}
