using System.Numerics;
using XivHud.Vitals.Core;

public sealed class VitalsTests
{
    [Theory]
    [InlineData(33u, "CP")]
    [InlineData(32u, "GP")]
    [InlineData(30u, "MP")]
    [InlineData(31u, "MP")]
    [InlineData(0u, "MP")]
    public void DisciplineUsesTheSamePoolAsTheCharacterRecord(uint category, string label)
        => Assert.Equal(label, Resource.SecondaryLabel(category));

    [Fact]
    public void SignedPartyMpCannotWrapNegativeValuesIntoHugePools()
    {
        Assert.Equal(new Resource("MP", 0, 10000), Resource.FromSigned("MP", -1, 10000));
        Assert.False(Resource.FromSigned("MP", 100, -1).Known);
    }

    [Fact]
    public void UnknownHealthIsDistinctFromDeathAndDoesNotDivideByZero()
    {
        var unknown = new Resource("HP", 0, 0);
        var defeated = new Resource("HP", 0, 100);
        Assert.False(unknown.Known); Assert.Equal("Unavailable", unknown.Display);
        Assert.True(defeated.Known); Assert.NotEqual("Unavailable", defeated.Display);
        Assert.Equal(0, unknown.Fraction); Assert.Equal(0, defeated.Fraction);
    }

    [Theory]
    [InlineData(50u, 100u, .5f)]
    [InlineData(200u, 100u, 1f)]
    [InlineData(uint.MaxValue, uint.MaxValue, 1f)]
    [InlineData(uint.MaxValue, 0u, 0f)]
    public void ResourceBarsStayBounded(uint current, uint maximum, float expected)
        => Assert.Equal(expected, new Resource("HP", current, maximum).Fraction);

    [Theory]
    [InlineData(false, 1f, 2f)]
    [InlineData(true, -1f, 2f)]
    [InlineData(true, 2f, 2f)]
    [InlineData(true, 3f, 2f)]
    [InlineData(true, 0f, 0f)]
    [InlineData(true, float.NaN, 2f)]
    [InlineData(true, 0f, float.PositiveInfinity)]
    public void FinishedOrInvalidCastsAreNotShown(bool casting, float elapsed, float total)
        => Assert.Null(CastReading.Create(casting, "Spell", elapsed, total, true));

    [Fact]
    public void CastInterpolationCannotRunFarAheadOfTheLastReading()
    {
        var cast = CastReading.Create(true, "", 1, 4, true)!;
        Assert.Equal("Casting", cast.Name); Assert.True(cast.Interruptible);
        Assert.Equal(.3125f, cast.Progress(10));
        Assert.Equal(2.75f, cast.Remaining(10));
        Assert.Equal(.25f, cast.Progress(float.NaN));
        Assert.Equal(.25f, cast.Progress(-1));
        var ending = CastReading.Create(true, "Spell", 3.9f, 4, false)!;
        Assert.Equal(1, ending.Progress(.25f)); Assert.Equal(0, ending.Remaining(.25f));
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(500, true)]
    [InlineData(501, false)]
    [InlineData(9000, false)]
    public void StaleOrFutureSnapshotsCannotMasqueradeAsLive(long age, bool expected)
        => Assert.Equal(expected, VitalsPreview.Create(1000).IsFresh(1000 + age));

    [Fact]
    public void EightPersonPreviewIncludesUnknownAndDefeatedCompanions()
    {
        var preview = VitalsPreview.Create(42, true);
        Assert.Equal(42, preview.ObservedAt); Assert.Equal(8, preview.Party.Count);
        Assert.Single(preview.Party, m => m.Self); Assert.Single(preview.Party, m => m.Leader);
        Assert.Contains(preview.Party, m => !m.Here && !m.Actor.Hp.Known && !m.Actor.Dead);
        Assert.Contains(preview.Party, m => m.Here && m.Actor.Hp.Known && m.Actor.Dead);
        Assert.True(preview.Target!.Cast!.Interruptible);
    }

    [Theory]
    [InlineData(640, 360, .65f)]
    [InlineData(1280, 720, 1f)]
    [InlineData(1920, 1080, 2f)]
    [InlineData(3840, 2160, 1.5f)]
    [InlineData(600, 900, 2f)]
    [InlineData(100, 80, 2f)]
    [InlineData(3840, 2160, 4f)]
    public void EveryPanelFitsAfterResolutionScaleAndPartySizeChanges(int width, int height, float scale)
    {
        var origin = new Vector2(231, -93); var viewport = new Vector2(width, height);
        foreach (var kind in Enum.GetValues<PanelKind>())
        foreach (var count in new[] { 0, 1, 4, 8, 24 })
        foreach (var anchor in new[] { Vector2.Zero, Vector2.One, new Vector2(.5f), new Vector2(5, -9), new Vector2(float.NaN) })
        {
            var rect = PanelLayout.Fit(kind, count, origin, viewport, anchor, scale);
            Assert.True(rect.Scale > 0);
            Assert.InRange(rect.Position.X, origin.X - .001f, origin.X + width - rect.Size.X + .001f);
            Assert.InRange(rect.Position.Y, origin.Y - .001f, origin.Y + height - rect.Size.Y + .001f);
            Assert.InRange(rect.Size.X, 0, width + .001f); Assert.InRange(rect.Size.Y, 0, height + .001f);
        }
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(.3f, .9f)]
    [InlineData(1f, 1f)]
    public void MovingPanelsRoundTripsNormalizedAnchors(float x, float y)
    {
        var anchor = new Vector2(x, y); var viewport = new Vector2(1920, 1080); var origin = new Vector2(-1920, 42);
        var rect = PanelLayout.Fit(PanelKind.Player, 0, origin, viewport, anchor, 1);
        var restored = PanelLayout.AnchorFromPosition(rect.Position, origin, viewport, rect.Size);
        Assert.Equal(anchor.X, restored.X, 5); Assert.Equal(anchor.Y, restored.Y, 5);
    }

    [Fact]
    public void DraggingBeyondEdgesClampsAndZeroTravelDoesNotProduceNan()
    {
        Assert.Equal(new Vector2(1, 0), PanelLayout.AnchorFromPosition(new(9999, -9999), Vector2.Zero, new(1280, 720), new(404, 166)));
        Assert.Equal(Vector2.Zero, PanelLayout.AnchorFromPosition(new(50), Vector2.Zero, new(100), new(100)));
    }

    [Fact]
    public void InvalidViewportsDoNotProduceDrawCoordinates()
    {
        foreach (var viewport in new[] { Vector2.Zero, new Vector2(-1, 300), new Vector2(float.NaN), new Vector2(float.PositiveInfinity) })
            Assert.Equal(default, PanelLayout.Fit(PanelKind.Player, 0, Vector2.Zero, viewport, Vector2.One, 1));
        var fit = PanelLayout.Fit(PanelKind.Player, 0, Vector2.Zero, new(1280, 720), new(float.NaN), float.NaN);
        Assert.Equal(1, fit.Scale); Assert.True(float.IsFinite(fit.Position.X));
    }
}
