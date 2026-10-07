using XivHud.Core;
using Xunit;

namespace XivHud.Core.Tests;

public sealed class HudVisibilityLeaseTests
{
    private static readonly HudModuleIdentity Initial = new(0x1000, 0x2000);

    [Fact]
    public void MaskHidesFourHudGroupsWithoutDisablingShortcutsOrSystemMenu()
    {
        Assert.Equal(2 | 4 | 8 | 16, HudVisibilityLease.Mask);
        Assert.Equal(0, HudVisibilityLease.Mask & (1 | 64));
    }

    [Fact]
    public void RepeatedOnThenOffBalancesExactlyOneNativeOperation()
    {
        var lease = new HudVisibilityLease();
        var balance = 0;
        Assert.True(lease.TryAcquire(Initial, () => { balance++; return true; }));
        Assert.False(lease.TryAcquire(Initial, () => { balance++; return true; }));
        Assert.Equal(1, balance);
        Assert.True(lease.IsHeld);
        Assert.Equal(Initial, lease.Identity);
        Assert.True(lease.Release(Initial, () => balance--));
        Assert.False(lease.Release(Initial, () => balance--));
        Assert.Equal(0, balance);
        Assert.False(lease.IsHeld);
    }

    [Fact]
    public void DisableAfterFailedAcquireCannotRestoreSomeoneElsesSuppression()
    {
        var lease = new HudVisibilityLease();
        var restores = 0;
        Assert.False(lease.TryAcquire(Initial, () => false));
        Assert.False(lease.Release(Initial, () => restores++));
        Assert.False(lease.IsHeld);
        Assert.Equal(0, restores);
        Assert.True(lease.TryAcquire(Initial, () => true)); // later ready native owner
    }

    [Fact]
    public void ThrowingAcquireDoesNotLeaveAnOwnedLease()
    {
        var lease = new HudVisibilityLease();
        Assert.Throws<InvalidOperationException>(() =>
            lease.TryAcquire(Initial, () => throw new InvalidOperationException()));
        Assert.False(lease.IsHeld);
        Assert.False(lease.Release(Initial, () => throw new Exception("Unexpected restore")));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4096, 0)]
    [InlineData(0, 8192)]
    public void IncompleteNativeIdentityNeverInvokesAcquire(long module, long manager)
    {
        var lease = new HudVisibilityLease();
        var invalid = new HudModuleIdentity((nint)module, (nint)manager);
        Assert.False(lease.TryAcquire(invalid, () => throw new Exception("Unsafe native operation")));
        Assert.False(lease.IsHeld);
    }

    [Theory]
    [InlineData(12288, 8192)]
    [InlineData(4096, 16384)]
    [InlineData(12288, 16384)]
    public void ReplacementOfEitherOwnerCannotRestoreThroughNewPointers(long module, long manager)
    {
        var lease = new HudVisibilityLease();
        lease.TryAcquire(Initial, () => true);
        var replacement = new HudModuleIdentity((nint)module, (nint)manager);
        Assert.False(lease.Release(replacement, () => throw new Exception("Stale native restore")));
        Assert.False(lease.IsHeld);
        Assert.True(lease.TryAcquire(replacement, () => true));
        Assert.Equal(replacement, lease.Identity);
    }

    [Fact]
    public void TemporarilyAbsentOwnerKeepsPendingReleaseUntilSamePairReturns()
    {
        var lease = new HudVisibilityLease();
        lease.TryAcquire(Initial, () => true);
        lease.Reconcile(default);
        Assert.False(lease.Release(default, () => throw new Exception("Null native restore")));
        Assert.False(lease.TryAcquire(default, () => throw new Exception("Null native acquire")));
        Assert.True(lease.IsHeld);
        var restores = 0;
        Assert.True(lease.Release(Initial, () => restores++));
        Assert.Equal(1, restores);
    }

    [Fact]
    public void AcquireWhileEnabledReconcilesAConcreteModuleReplacement()
    {
        var lease = new HudVisibilityLease();
        var acquisitions = 0;
        lease.TryAcquire(Initial, () => { acquisitions++; return true; });
        var replacement = new HudModuleIdentity(0x3000, 0x4000);
        Assert.True(lease.TryAcquire(replacement, () => { acquisitions++; return true; }));
        Assert.Equal(2, acquisitions);
        Assert.Equal(replacement, lease.Identity);
        Assert.False(lease.TryAcquire(replacement, () => throw new Exception("Duplicate suppression")));
    }

    [Fact]
    public void RestoreFailureCannotBeRetriedByUpdateOrShutdown()
    {
        var lease = new HudVisibilityLease();
        lease.TryAcquire(Initial, () => true);
        var restores = 0;
        Assert.Throws<InvalidOperationException>(() => lease.Release(Initial, () =>
        {
            restores++;
            Assert.False(lease.IsHeld); // ownership retired before entering native code
            throw new InvalidOperationException();
        }));
        Assert.False(lease.Release(Initial, () => restores++));
        lease.Drop();
        Assert.False(lease.Release(Initial, () => restores++));
        Assert.Equal(1, restores);
    }

    [Fact]
    public void ShutdownWithoutValidModuleDropsPendingLeaseWithoutLateRestore()
    {
        var lease = new HudVisibilityLease();
        lease.TryAcquire(Initial, () => true);
        Assert.False(lease.Release(default, () => throw new Exception("Unavailable native owner")));
        lease.Drop();
        Assert.False(lease.Release(Initial, () => throw new Exception("Restore after shutdown")));
        Assert.False(lease.IsHeld);
    }

    [Fact]
    public void NewEnableEpisodeAfterSuccessfulReleaseAcquiresAgain()
    {
        var lease = new HudVisibilityLease();
        var acquisitions = 0;
        for (var episode = 0; episode < 3; episode++)
        {
            Assert.True(lease.TryAcquire(Initial, () => { acquisitions++; return true; }));
            Assert.True(lease.Release(Initial, () => { }));
        }
        Assert.Equal(3, acquisitions);
    }
}
