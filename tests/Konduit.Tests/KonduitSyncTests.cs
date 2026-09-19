using Konduit;

namespace Konduit.Tests;

public sealed class KonduitSyncTests
{
    [Fact]
    public void Wait_OnACompletedTask_Returns() => KonduitSync.Wait(default);

    [Fact]
    public void Wait_OnAFaultedTask_RethrowsTheOriginalException()
    {
        var pending = new ValueTask(Task.FromException(new InvalidOperationException("boom")));

        var ex = Assert.Throws<InvalidOperationException>(() => KonduitSync.Wait(pending));

        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public void Wait_OnAnIncompleteTask_BlocksUntilItCompletes()
    {
        var source = new TaskCompletionSource();
        var pending = new ValueTask(source.Task);
        _ = Task.Run(async () =>
        {
            await Task.Delay(20);
            source.SetResult();
        });

        KonduitSync.Wait(pending);

        Assert.True(source.Task.IsCompletedSuccessfully);
    }
}
