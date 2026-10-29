using System.Numerics;
using XivHud.Windows.Core;
namespace XivHud.Windows.Tests;
public class PanelGeometryTests
{
    [Fact] public void ViewPlacementKeepsTheSourceAspectRatio()
    {
        var fitted = WindowLayout.Fit(new(1371,889), new(1344,756));
        Assert.Equal(756, fitted.Y, .01);
        Assert.InRange(Math.Abs(fitted.X / fitted.Y - 1371f / 889), 0, .001);
        Assert.Equal(new Vector2(400,300), WindowLayout.Fit(new(400,300), new(1344,756)));
    }
    [Fact] public void RecoveryBringsAnOffscreenSourceBackIntoView()
    { Assert.Equal(Vector2.Zero, WindowLayout.Recover(new(-460, -160), new(760,520), Vector2.Zero, new(1920,1080))); }
    [Fact] public void RecoveryPreservesValidPositionAndHandlesAnotherViewport()
    {
        Assert.Equal(new Vector2(200,300), WindowLayout.Recover(new(200,300), new(760,520), Vector2.Zero, new(1920,1080)));
        Assert.Equal(new Vector2(1920,560), WindowLayout.Recover(new(1800,900), new(760,520), new(1920,0), new(1920,1080)));
    }
    [Fact] public void OversizedAndNonFiniteSourcesRetainAVisibleOrigin()
    {
        Assert.Equal(Vector2.Zero, WindowLayout.Recover(new(100), new(4096), Vector2.Zero, new(1920,1080)));
        Assert.Equal(Vector2.Zero, WindowLayout.Recover(new(float.NaN), new(760,520), Vector2.Zero, new(1920,1080)));
    }
    [Fact] public void ProjectedQuadMapsBothTrianglesAndRejectsOutside()
    {
        var q = new PanelQuad(new(100, 100), new(500, 140), new(450, 400), new(150, 440));
        Assert.True(q.TryUv(q.A, out var a)); Assert.Equal(Vector2.Zero, a);
        Assert.True(q.TryUv(q.C, out var c)); Assert.Equal(Vector2.One, c);
        Assert.True(q.TryUv((q.A + q.B + q.C) / 3, out var first));
        Assert.True(Vector2.Distance(first, new(2f/3, 1f/3)) < .001);
        Assert.True(q.TryUv((q.A + q.C + q.D) / 3, out var second));
        Assert.True(Vector2.Distance(second, new(1f/3, 2f/3)) < .001);
        Assert.False(q.TryUv(Vector2.Zero, out _));
    }
    [Fact] public void InvalidAndDegenerateGeometryCannotCaptureInput()
    {
        Assert.False(new PanelQuad().TryUv(Vector2.Zero, out _));
        Assert.False(new PanelQuad(new(float.NaN), new(300), new(400), new(500)).TryUv(new(350), out _));
        var q = new PanelQuad(new(0), new(100,0), new(200,0), new(300,0));
        Assert.False(q.TryUv(new(20,0), out _));
    }
    [Theory]
    [InlineData("Inventory###XivHud.Inventory", true)]
    [InlineData("Changing title###XivHud.Inventory", true)]
    [InlineData("A normal plugin window", true)]
    [InlineData("##internal", false)]
    [InlineData("Title###", false)]
    [InlineData(" Title###id", false)]
    [InlineData("Title\n###id", false)]
    public void ExactNamesExcludeInternalAndMalformedWindows(string name, bool valid) => Assert.Equal(valid, WindowIdentity.Valid(name));
    [Fact] public void DynamicTitlesRetainTheSameOwnerKey()
    { Assert.Equal(WindowIdentity.Key("First###module"), WindowIdentity.Key("Second###module")); }
}
