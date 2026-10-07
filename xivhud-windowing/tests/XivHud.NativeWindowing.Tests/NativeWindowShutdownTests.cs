using XivHud.Windowing.Dalamud;

namespace XivHud.NativeWindowing.Tests;

public sealed class NativeWindowShutdownTests
{
    [Fact]
    public async Task RetryRetirementStillBlocksUnloadAfterCurrentBackendFinishes()
    {
        var shutdown = new NativeWindowShutdown();
        var previous = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var current = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        shutdown.Track(previous.Task);
        shutdown.Track(current.Task);
        var unloading = shutdown.Completion;
        current.SetResult();
        Assert.False(unloading.IsCompleted);
        previous.SetResult();
        await unloading;
    }

    [Fact]
    public async Task FailedEarlierRetirementIsNotHiddenBySuccessfulNewBackend()
    {
        var shutdown = new NativeWindowShutdown();
        var failed = Task.FromException(new InvalidOperationException("Native close failed"));
        shutdown.Track(failed);
        shutdown.Track(Task.CompletedTask);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => shutdown.Completion);
        Assert.Equal("Native close failed", error.Message);
    }

    [Fact]
    public async Task AnOlderFailureStillWaitsForOtherNativeCallbacksToRetire()
    {
        var shutdown = new NativeWindowShutdown();
        shutdown.Track(Task.FromException(new InvalidOperationException("Native close failed")));
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        shutdown.Track(pending.Task);
        Assert.False(shutdown.Completion.IsCompleted);
        pending.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => shutdown.Completion);
    }
}
