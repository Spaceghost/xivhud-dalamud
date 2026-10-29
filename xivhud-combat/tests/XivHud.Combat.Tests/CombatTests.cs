using XivHud.Combat.Core;

namespace XivHud.Combat.Tests;

public sealed class CombatTests
{
    private static SlotIdentity Slot(long observed = 1000) => new(12, 19, false, 0, 4, 1, 3538, observed);
    [Fact]
    public void AClickOnlyMatchesTheSameCharacterJobAndAssignment()
    {
        var intended = Slot();
        Assert.True(ManualActionGate.CanExecute(intended, intended, 1100, true, false));
        foreach (var changed in new[] { intended with { Player = 13 }, intended with { Job = 20 }, intended with { Pvp = true }, intended with { Bar = 1 }, intended with { Slot = 5 }, intended with { Type = 2 }, intended with { Command = 9 } })
            Assert.False(ManualActionGate.CanExecute(intended, changed, 1100, true, false));
    }
    [Theory]
    [InlineData(999)] [InlineData(1501)]
    public void OldAndFutureClicksAreRejected(long now) => Assert.False(ManualActionGate.CanExecute(Slot(), Slot(), now, true, false));
    [Theory]
    [InlineData(false, false)] [InlineData(true, true)]
    public void DisabledAndPreviewNeverActivateActions(bool enabled, bool preview) => Assert.False(ManualActionGate.CanExecute(Slot(), Slot(), 1100, enabled, preview));
    [Theory]
    [InlineData(-1, 0)] [InlineData(18, 0)] [InlineData(0, 12)] [InlineData(10, 16)] [InlineData(0, -1)]
    public void NativeBarBoundsAreEnforced(int bar, int slot)
    {
        var value = Slot() with { Bar = bar, Slot = slot };
        Assert.False(ManualActionGate.CanExecute(value, value, 1100, true, false));
    }
    [Fact]
    public void EmptySlotsAndMissingPlayerAreRejected()
    {
        foreach (var slot in new[] { Slot() with { Type = 0 }, Slot() with { Player = 0 } })
            Assert.False(ManualActionGate.CanExecute(slot, slot, 1100, true, false));
    }
    [Fact]
    public void ChargesShowNextChargeAndFullRechargeSeparately()
    {
        var recast = Assert.IsType<Cooldown>(Cooldown.Create(60, 35, 1, 2));
        Assert.Equal(25, recast.FullRemaining(0)); Assert.Equal(25, recast.NextRemaining(0));
        Assert.Equal(24.75f, recast.NextRemaining(100));
        var empty = Assert.IsType<Cooldown>(Cooldown.Create(60, 10, 0, 2));
        Assert.Equal(50, empty.FullRemaining(0)); Assert.Equal(20, empty.NextRemaining(0));
    }
    [Fact]
    public void InterpolationIsBoundedAndDoesNotInventAnotherCharge()
    {
        var recast = Assert.IsType<Cooldown>(Cooldown.Create(60, 59.9f, 1, 2));
        Assert.Equal(0, recast.NextRemaining(100)); Assert.Equal(1U, recast.Charges);
        Assert.Equal(recast.FullRemaining(0), recast.FullRemaining(float.NaN));
    }
    [Theory]
    [InlineData(float.NaN, 0, 0, 1)] [InlineData(2, -1, 0, 1)] [InlineData(2, 3, 0, 1)]
    [InlineData(60, 0, 3, 2)] [InlineData(60, 0, 0, 0)]
    public void InvalidNativeCooldownsAreUnknown(float total, float elapsed, uint charges, uint max) => Assert.Null(Cooldown.Create(total, elapsed, charges, max));
    [Theory]
    [InlineData(false, 1, 3, 36)] [InlineData(true, 1, 2, 32)] [InlineData(false, 10, 3, 12)] [InlineData(true, 8, 3, 16)]
    public void PreviewIsBoundedToRealBarRanges(bool cross, int first, int rows, int count)
    {
        var preview = CombatPreview.Create(1000, cross, first, rows);
        Assert.Equal(count, preview.Slots.Count); Assert.True(preview.IsFresh(1500)); Assert.False(preview.IsFresh(1501)); Assert.False(preview.IsFresh(999));
        Assert.All(preview.Slots, s => Assert.False(ManualActionGate.CanExecute(s.Identity, s.Identity, 1000, true, true)));
    }
}
