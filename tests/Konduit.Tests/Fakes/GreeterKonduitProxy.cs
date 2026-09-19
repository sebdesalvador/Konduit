using System.Runtime.CompilerServices;
using Konduit;

namespace Konduit.Tests.Fakes;

/// <summary>
/// A hand-written stand-in for what the source generator will emit in slice 3. Every shape the
/// generator has to handle appears here once: a synchronous value-returning method, an asynchronous
/// one, a void one, and a <see cref="SkipKonduitAttribute"/> pass-through.
/// </summary>
internal sealed class GreeterKonduitProxy(IGreeter target, KonduitDelegate pipeline, IServiceProvider services)
    : IGreeter, IKonduitProxy
{
    [ModuleInitializer]
    internal static void Register() =>
        KonduitProxyRegistry.Register(
            typeof(IGreeter),
            static (target, pipeline, services) => new GreeterKonduitProxy((IGreeter)target, pipeline, services));

    private static readonly KonduitMethod GreetMethod = new(
        typeof(IGreeter),
        nameof(IGreeter.Greet),
        typeof(string),
        [new KonduitParameter("name", typeof(string), 0)]);

    private static readonly KonduitDelegate GreetTerminal = static context =>
    {
        context.Result = ((IGreeter)context.Target).Greet((string)context.Arguments[0]!);
        return default;
    };

    public string Greet(string name)
    {
        var context = new KonduitContext(target, GreetMethod, [name], services, GreetTerminal);
        KonduitSync.Wait(pipeline(context));
        return (string)context.Result!;
    }

    private static readonly KonduitMethod GreetAsyncMethod = new(
        typeof(IGreeter),
        nameof(IGreeter.GreetAsync),
        typeof(Task<string>),
        [
            new KonduitParameter("name", typeof(string), 0),
            new KonduitParameter("cancellationToken", typeof(CancellationToken), 1),
        ]);

    private static readonly KonduitDelegate GreetAsyncTerminal = static async context =>
        context.Result = await ((IGreeter)context.Target)
            .GreetAsync((string)context.Arguments[0]!, (CancellationToken)context.Arguments[1]!)
            .ConfigureAwait(false);

    public async Task<string> GreetAsync(string name, CancellationToken cancellationToken)
    {
        var context = new KonduitContext(
            target,
            GreetAsyncMethod,
            [name, cancellationToken],
            services,
            GreetAsyncTerminal)
        {
            CancellationToken = cancellationToken,
        };

        await pipeline(context).ConfigureAwait(false);
        return (string)context.Result!;
    }

    private static readonly KonduitMethod RecordMethod = new(
        typeof(IGreeter),
        nameof(IGreeter.Record),
        typeof(void),
        [new KonduitParameter("name", typeof(string), 0)]);

    private static readonly KonduitDelegate RecordTerminal = static context =>
    {
        ((IGreeter)context.Target).Record((string)context.Arguments[0]!);
        return default;
    };

    public void Record(string name) =>
        KonduitSync.Wait(pipeline(new KonduitContext(target, RecordMethod, [name], services, RecordTerminal)));

    public string GreetDirectly(string name) => target.GreetDirectly(name);

    public object KonduitTarget => target;
}
