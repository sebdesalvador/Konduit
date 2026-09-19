using Konduit;
using Konduit.Tests.Fakes;

namespace Konduit.Tests;

public sealed class KonduitMethodTests
{
    [Fact]
    public void MethodInfo_ResolvesTheDeclaredInterfaceMethod()
    {
        var method = TestContext.GreetMethod;

        var resolved = method.MethodInfo;

        Assert.Equal(nameof(IGreeter.Greet), resolved.Name);
        Assert.Equal(typeof(IGreeter), resolved.DeclaringType);
        Assert.Same(resolved, method.MethodInfo);
    }

    [Fact]
    public void MethodInfo_WhenTheMethodCannotBeFound_ThrowsWithAHelpfulMessage()
    {
        var method = new KonduitMethod(typeof(IGreeter), "NoSuchMethod", typeof(void), []);

        var ex = Assert.Throws<InvalidOperationException>(() => method.MethodInfo);

        Assert.Contains("NoSuchMethod", ex.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(IGreeter), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parameters_AreExposedInDeclarationOrder()
    {
        var parameter = Assert.Single(TestContext.GreetMethod.Parameters);

        Assert.Equal("name", parameter.Name);
        Assert.Equal(typeof(string), parameter.Type);
        Assert.Equal(0, parameter.Position);
    }
}
