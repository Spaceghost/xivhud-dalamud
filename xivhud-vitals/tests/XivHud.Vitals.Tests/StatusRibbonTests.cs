using XivHud.Vitals.Core;

public sealed class StatusRibbonTests
{
    private static StatusReading Effect(int slot, float remaining = 12) => new(slot, 1, "Effect", 0, remaining, false, false, false, 0);

    [Theory]
    [InlineData(0f, "—")]
    [InlineData(-10f, "—")]
    [InlineData(float.NaN, "—")]
    [InlineData(float.PositiveInfinity, "—")]
    [InlineData(.1f, "<1s")]
    [InlineData(12f, "12s")]
    [InlineData(61f, "1:01")]
    [InlineData(3601f, "1h 00m")]
    public void TimersDistinguishUnknownFromPermanentAndBoundInterpolation(float remaining, string expected)
        => Assert.Equal(expected, Effect(0, remaining).Duration(999));

    [Fact]
    public void PermanentMetadataAndBadAgesCannotInventAnExpiry()
    {
        Assert.Equal("∞", (Effect(0, float.NaN) with { Permanent = true }).Duration(1));
        Assert.Equal("12s", Effect(0).Duration(float.NaN));
        Assert.Equal("12s", Effect(0).Duration(-99));
    }

    [Theory]
    [InlineData(3, 5, 3)]
    [InlineData(1, 1, 0)]
    [InlineData(99, 5, 0)]
    [InlineData(4, 0, 0)]
    [InlineData(-1, 5, 0)]
    public void NonStackParametersAreNeverPresentedAsStacks(int parameter, int maximum, int expected)
        => Assert.Equal(expected, StatusReading.StackCount(parameter, maximum));

    [Fact]
    public void DispellableFirstPreservesSlotOrderAndEffectsFromDifferentSources()
    {
        var selection = StatusRibbon.Select([Effect(3), Effect(2) with { Dispellable = true }, Effect(0), Effect(1)], false);
        Assert.Equal(new[] { 2, 0, 1, 3 }, selection.Visible.Select(s => s.Slot));
        Assert.Equal(4, selection.Visible.Count); // Same ID can legitimately occupy multiple slots.
    }

    [Fact]
    public void OwnedFilterRunsBeforeCapacityAndReportsOnlyHiddenMatchingEffects()
    {
        var effects = Enumerable.Range(0, 24).Select(i => Effect(i) with { Mine = i % 2 == 0 }).ToArray();
        var selection = StatusRibbon.Select(effects, true);
        Assert.Equal(8, selection.Visible.Count); Assert.Equal(4, selection.Hidden);
        Assert.All(selection.Visible, s => Assert.True(s.Mine));
        Assert.Empty(StatusRibbon.Select([Effect(0) with { Id = 0 }], false).Visible);
    }

    [Fact]
    public void PreviewCoversUnknownPermanentDispellableStackedAndOverflowStates()
    {
        var state = VitalsPreview.Create(10);
        Assert.Contains(state.PlayerEffects!, s => s.Permanent);
        Assert.Contains(state.PlayerEffects!, s => s.Dispellable);
        Assert.Contains(state.PlayerEffects!, s => s.Stacks > 0);
        Assert.Contains(state.PlayerEffects!, s => !s.Permanent && s.RemainingSeconds == 0);
        Assert.Equal(3, StatusRibbon.Select(state.TargetEffects!, false).Hidden);
    }
}
