using Konduit;
using Konduit.Tests.Fakes;

namespace Konduit.Tests;

public sealed class KonduitContextTests
{
    [Fact]
    public void Items_AreLazyButStable()
    {
        var context = TestContext.For(new Greeter(), "Ada");

        var first = context.Items;

        Assert.Empty(first);
        Assert.Same(first, context.Items);
    }

    [Fact]
    public void CancellationToken_DefaultsToNone()
    {
        var context = TestContext.For(new Greeter(), "Ada");

        Assert.Equal(CancellationToken.None, context.CancellationToken);
    }

    [Fact]
    public void CancellationToken_CanBeSuppliedByTheProxy()
    {
        using var cts = new CancellationTokenSource();
        var context = new KonduitContext(
            new Greeter(),
            TestContext.GreetMethod,
            ["Ada"],
            EmptyServiceProvider.Instance,
            static _ => default)
        {
            CancellationToken = cts.Token,
        };

        Assert.Equal(cts.Token, context.CancellationToken);
    }

    [Theory]
    [InlineData("target")]
    [InlineData("method")]
    [InlineData("arguments")]
    [InlineData("services")]
    [InlineData("terminal")]
    public void Constructor_RejectsNullArguments(string nullArgument)
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new KonduitContext(
            nullArgument == "target" ? null! : new Greeter(),
            nullArgument == "method" ? null! : TestContext.GreetMethod,
            nullArgument == "arguments" ? null! : ["Ada"],
            nullArgument == "services" ? null! : EmptyServiceProvider.Instance,
            nullArgument == "terminal" ? null! : static _ => default));

        Assert.Equal(nullArgument, ex.ParamName);
    }
}
