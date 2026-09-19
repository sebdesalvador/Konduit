using Konduit;
using Konduit.Tests.Fakes;

namespace Konduit.Tests;

public sealed class KonduitProxyRegistryTests
{
    [Fact]
    public void ModuleInitializer_RegistersTheProxyFactory() =>
        Assert.True(KonduitProxyRegistry.IsRegistered(typeof(IGreeter)));

    [Fact]
    public void IsRegistered_ForAnUnknownService_IsFalse() =>
        Assert.False(KonduitProxyRegistry.IsRegistered(typeof(IDisposable)));

    [Fact]
    public void Register_RejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => KonduitProxyRegistry.Register(null!, (_, _, _) => new object()));
        Assert.Throws<ArgumentNullException>(() => KonduitProxyRegistry.Register(typeof(IGreeter), null!));
    }

    [Fact]
    public void Register_IsIdempotentForTheSameService()
    {
        KonduitProxyRegistry.Register(
            typeof(IGreeter),
            static (target, pipeline, services) => new GreeterKonduitProxy((IGreeter)target, pipeline, services));

        Assert.True(KonduitProxyRegistry.IsRegistered(typeof(IGreeter)));
    }
}
