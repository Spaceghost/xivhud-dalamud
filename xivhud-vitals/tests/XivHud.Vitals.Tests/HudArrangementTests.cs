using System.Numerics;
using XivHud.Vitals.Core;

public sealed class HudArrangementTests
{
    [Theory]
    [InlineData(640, 360, 1f)]
    [InlineData(1280, 720, 1.5f)]
    [InlineData(1920, 1080, 3f)]
    [InlineData(3840, 2160, 2f)]
    [InlineData(2560, 720, 2f)]
    [InlineData(720, 1280, 1f)]
    [InlineData(100, 80, 8f)]
    public void SixPanelsStayOnScreenWithoutOverlapOrCoveringTheCentre(int width, int height, float scale)
    {
        var origin = new Vector2(-1920, 48); var viewport = new Vector2(width, height);
        foreach (var count in new[] { 0, 1, 4, 8, 99 })
        {
            var rectangles = Enum.GetValues<PanelKind>().Select(kind => HudArrangement.Fit(kind, count, origin, viewport, scale)).ToArray();
            foreach (var rect in rectangles)
            {
                Assert.True(rect.Scale > 0);
                Assert.InRange(rect.Position.X, origin.X, origin.X + viewport.X - rect.Size.X + .001f);
                Assert.InRange(rect.Position.Y, origin.Y, origin.Y + viewport.Y - rect.Size.Y + .001f);
                var centre = origin + viewport / 2;
                Assert.False(centre.X > rect.Position.X && centre.X < rect.Position.X + rect.Size.X &&
                    centre.Y > rect.Position.Y && centre.Y < rect.Position.Y + rect.Size.Y);
            }
            for (var i = 0; i < rectangles.Length; i++)
            for (var j = i + 1; j < rectangles.Length; j++)
            {
                var a = rectangles[i]; var b = rectangles[j];
                Assert.True(a.Position.X + a.Size.X <= b.Position.X || b.Position.X + b.Size.X <= a.Position.X ||
                    a.Position.Y + a.Size.Y <= b.Position.Y || b.Position.Y + b.Size.Y <= a.Position.Y);
            }
        }
    }

    [Fact]
    public void InvalidViewportsFailClosedAndBadPreferencesRecover()
    {
        Assert.Equal(default, HudArrangement.Fit(PanelKind.Player, 8, Vector2.Zero, new(float.NaN), 1));
        Assert.Equal(default, HudArrangement.Fit(PanelKind.Player, 8, new(float.PositiveInfinity), new(1920, 1080), 1));
        var normal = HudArrangement.Fit(PanelKind.Player, 8, Vector2.Zero, new(1920, 1080), 1);
        Assert.Equal(normal, HudArrangement.Fit(PanelKind.Player, 8, Vector2.Zero, new(1920, 1080), float.NaN));
    }
}
