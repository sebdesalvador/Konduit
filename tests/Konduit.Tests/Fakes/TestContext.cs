using Konduit;

namespace Konduit.Tests.Fakes;

/// <summary>Builds contexts the way a generated proxy would, for tests that exercise the pipeline alone.</summary>
internal static class TestContext
{
    public static readonly KonduitMethod GreetMethod = new(
        typeof(IGreeter),
        nameof(IGreeter.Greet),
        typeof(string),
        [new KonduitParameter("name", typeof(string), 0)]);

    public static KonduitContext For(
        IGreeter target,
        string name,
        IServiceProvider? services = null,
        KonduitDelegate? terminal = null) =>
        new(
            target,
            GreetMethod,
            [name],
            services ?? EmptyServiceProvider.Instance,
            terminal ?? DefaultTerminal);

    private static readonly KonduitDelegate DefaultTerminal = static context =>
    {
        context.Result = ((IGreeter)context.Target).Greet((string)context.Arguments[0]!);
        return default;
    };
}
