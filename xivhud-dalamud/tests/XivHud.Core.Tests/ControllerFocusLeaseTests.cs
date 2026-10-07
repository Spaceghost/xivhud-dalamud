using XivHud.Core;

namespace XivHud.Core.Tests;

public sealed class ControllerFocusLeaseTests
{
    private const string Owner = "XivHud.Journal";
    private static ControllerSample Sample(uint held = 0, float x = 0, ulong source = 1) => new(source, held, x, 0, 0, 0);
    private static ControllerFocusLease Active()
    {
        var lease = new ControllerFocusLease();
        Assert.True(lease.Renew(Owner, 1, 0));
        Assert.True(lease.Poll(Sample(), true, .001).All);
        Assert.True(lease.Poll(Sample(), true, .002).All);
        Assert.Equal("Active", lease.Read(Owner, 1, .003)!.Value.Phase);
        return lease;
    }

    [Fact]
    public void UnfocusedGameReceivesEveryButtonAndBothSticks()
    {
        var lease = new ControllerFocusLease();
        Assert.Equal(default, lease.Poll(new(1, 0xFFFF, 1, -1, -1, 1), true, 1));
        Assert.Null(lease.Read(Owner, 1, 1));
    }

    [Fact]
    public void HeldNativeOpenButtonCannotActivateJournal()
    {
        var lease = new ControllerFocusLease();
        lease.Renew(Owner, 1, 0);
        for (var i = 1; i <= 8; i++)
        {
            Assert.True(lease.Poll(Sample(32), true, i * .01).All);
            Assert.Equal("WaitingForNeutral", lease.Read(Owner, 1, i * .01)!.Value.Phase);
        }
        lease.Poll(Sample(), true, .081);
        Assert.Equal("WaitingForNeutral", lease.Read(Owner, 1, .081)!.Value.Phase);
        lease.Poll(Sample(), true, .082);
        Assert.Equal(0u, lease.Read(Owner, 1, .082)!.Value.Pressed);
        lease.Poll(Sample(32), true, .083);
        Assert.Equal(32u, lease.Read(Owner, 1, .083)!.Value.Pressed);
    }

    [Fact]
    public void DeflectedStickMustReturnToNeutralBeforeTakingFocus()
    {
        var lease = new ControllerFocusLease(); lease.Renew(Owner, 1, 0);
        lease.Poll(Sample(x: 1), true, .01);
        lease.Poll(Sample(x: 1), true, .02);
        Assert.Equal("WaitingForNeutral", lease.Read(Owner, 1, .03)!.Value.Phase);
        lease.Poll(Sample(x: .1f), true, .04); lease.Poll(Sample(x: .1f), true, .05);
        Assert.Equal("Active", lease.Read(Owner, 1, .06)!.Value.Phase);
    }

    [Fact]
    public void NativePressedRetriggerCannotRepeatHeldConfirm()
    {
        using var lease = Active();
        lease.Poll(Sample(32), true, .01);
        Assert.Equal(32u, lease.Read(Owner, 1, .011)!.Value.Pressed);
        lease.Poll(Sample(32), true, .02);
        var again = lease.Read(Owner, 1, .021)!.Value;
        Assert.Equal(32u, again.Held); Assert.Equal(0u, again.Pressed);
    }

    [Fact]
    public void QuickPressReleaseBetweenDrawsIsDeliveredOnce()
    {
        using var lease = Active();
        lease.Poll(Sample(64), true, .01); lease.Poll(Sample(), true, .02);
        var frame = lease.Read(Owner, 1, .03)!.Value;
        Assert.Equal(64u, frame.Pressed); Assert.Equal(64u, frame.Released); Assert.Equal(0u, frame.Held);
        var next = lease.Read(Owner, 1, .04)!.Value;
        Assert.Equal(0u, next.Pressed | next.Released);
    }

    [Fact]
    public void AnotherModuleCannotStealCurrentFocusOrReadItsInput()
    {
        using var lease = Active();
        Assert.False(lease.Renew("XivHud.Other", 2, .01));
        Assert.Null(lease.Read("XivHud.Other", 1, .02));
        Assert.False(lease.Release("XivHud.Other", 1, .03));
        Assert.NotNull(lease.Read(Owner, 1, .04));
    }

    [Fact]
    public void OldFocusGenerationCannotReleaseOrReadNewFocus()
    {
        using var lease = Active();
        Assert.False(lease.Release(Owner, 2, .01));
        Assert.Null(lease.Read(Owner, 2, .02));
        Assert.True(lease.Release(Owner, 1, .03));
        Assert.True(lease.Renew(Owner, 2, .04));
        Assert.False(lease.Release(Owner, 1, .05));
    }

    [Fact]
    public void ReloadedBrokerHasDistinctSessionEvenWithRepeatedSequence()
    {
        using var first = new ControllerFocusLease(100);
        using var reloaded = new ControllerFocusLease(200);
        foreach (var lease in new[] { first, reloaded })
        {
            lease.Renew(Owner, 1, 0);
            lease.Poll(Sample(), true, .001); lease.Poll(Sample(), true, .002);
        }
        var before = first.Read(Owner, 1, .003)!.Value;
        var after = reloaded.Read(Owner, 1, .003)!.Value;
        Assert.Equal(before.Sequence, after.Sequence);
        Assert.NotEqual(before.Session, after.Session);
    }

    [Fact]
    public void ClosingRestoresSticksAndNewButtonsWhileCancelDrains()
    {
        using var lease = Active();
        lease.Poll(Sample(128, 1), true, .01);
        Assert.True(lease.Release(Owner, 1, .02));
        var first = lease.Poll(Sample(128 | 32, 1), true, .03);
        Assert.False(first.All); Assert.Equal(128u, first.Buttons);
        var up = lease.Poll(Sample(32, 1), true, .04);
        Assert.Equal(128u, up.Buttons); // Absorb the old cancel release only.
        Assert.Equal(default, lease.Poll(Sample(32, 1), true, .05));
        Assert.Null(lease.Read(Owner, 1, .06));
    }

    [Fact]
    public void HeldCancelNeverBlocksGameBeyondHundredMilliseconds()
    {
        using var lease = Active();
        lease.Poll(Sample(128), true, .01); lease.Release(Owner, 1, .02);
        Assert.Equal(128u, lease.Poll(Sample(128), true, .11).Buttons);
        Assert.Equal(default, lease.Poll(Sample(128), true, .121));
    }

    [Fact]
    public void DrawStallExpiresCaptureWithoutRenewalOrRetimestamping()
    {
        using var lease = Active();
        Assert.True(lease.Poll(Sample(32, 1), true, .099).All);
        Assert.Equal(default, lease.Poll(Sample(32, 1), true, .101));
        Assert.Null(lease.Read(Owner, 1, .102));
    }

    [Fact]
    public void ReadingDoesNotRenewTheFocusLease()
    {
        using var lease = Active();
        Assert.NotNull(lease.Read(Owner, 1, .09));
        Assert.Null(lease.Read(Owner, 1, .101));
    }

    [Fact]
    public void RenewedUiCannotReadAStalledControllerPoll()
    {
        using var lease = Active();
        Assert.True(lease.Renew(Owner, 1, .09));
        Assert.Null(lease.Read(Owner, 1, .104));
    }

    [Fact]
    public void ForeignGlobalModeCutsceneOrDisconnectFailsOpen()
    {
        using var lease = Active();
        Assert.Equal(default, lease.Poll(Sample(0xFFFF, 1), false, .01));
        Assert.Null(lease.Read(Owner, 1, .02));
    }

    [Theory]
    [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)] [InlineData(1.01f)]
    public void InvalidNativeAxisNeverOwnsInput(float axis)
    {
        using var lease = Active();
        Assert.Equal(default, lease.Poll(Sample(32, axis), true, .01));
        Assert.Null(lease.Read(Owner, 1, .02));
    }

    [Fact]
    public void ChangedDeviceRequiresNeutralAndClearsOldEdges()
    {
        using var lease = Active(); lease.Poll(Sample(32), true, .01);
        lease.Poll(Sample(64, source: 2), true, .02);
        var waiting = lease.Read(Owner, 1, .03)!.Value;
        Assert.Equal("WaitingForNeutral", waiting.Phase); Assert.Equal(0u, waiting.Pressed);
    }

    [Fact]
    public void ClosingDoesNotQuarantineButtonsOnAReplacementController()
    {
        using var lease = Active(); lease.Poll(Sample(128), true, .01);
        lease.Release(Owner, 1, .02);
        Assert.Equal(default, lease.Poll(Sample(128, source: 2), true, .03));
        Assert.Null(lease.Read(Owner, 1, .04));
    }

    [Fact]
    public void ClockRollbackAndMalformedClockCannotRetainCapture()
    {
        using var lease = Active();
        Assert.Equal(default, lease.Poll(Sample(32), true, 0));
        Assert.False(lease.Renew(Owner, 1, double.NaN));
        Assert.Null(lease.Read(Owner, 1, 1));
    }

    [Fact]
    public void UnloadImmediatelyRestoresAllControllerInputs()
    {
        var lease = Active(); lease.Poll(Sample(128, 1), true, .01); lease.Dispose();
        Assert.Equal(default, lease.Poll(Sample(0xFFFF, 1), true, .02));
        Assert.False(lease.Renew(Owner, 1, .03));
        Assert.Null(lease.Read(Owner, 1, .04));
    }

    [Theory]
    [InlineData("")] [InlineData("OtherPlugin")] [InlineData("XivHud.bad/name")]
    public void InvalidOwnerNamesCannotAcquire(string name)
    {
        using var lease = new ControllerFocusLease();
        Assert.False(lease.Renew(name, 1, 0));
    }
}
