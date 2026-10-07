using System.Numerics;
using XivHud.Journal;

public sealed class JournalPopupLayoutTests
{
    [Theory]
    [InlineData(1080, 700, 1, 470, 650)]
    [InlineData(900, 630, 1, 470, 650)]
    [InlineData(720, 504, .8f, 620, 370)]
    [InlineData(2160, 1400, 2, 470, 650)]
    [InlineData(32, 32, .05f, 620, 370)]
    public void SettingsAndSearchRemainInsideCapturedContent(float width, float height, float scale, float pw, float ph)
    {
        var origin = new Vector2(351, 192); var size = new Vector2(width, height);
        var popup = JournalPopupLayout.Fit(origin, size, new(pw * scale, ph * scale), scale);
        Assert.True(popup.Size.X > 0 && popup.Size.Y > 0);
        Assert.True(popup.Position.X > origin.X && popup.Position.Y > origin.Y);
        Assert.True(popup.Position.X + popup.Size.X < origin.X + size.X);
        Assert.True(popup.Position.Y + popup.Size.Y < origin.Y + size.Y);
        Assert.Equal(origin + size / 2, popup.Position + popup.Size / 2);
    }
    [Fact] public void MovingNativeFrameMovesPopupWithoutChangingItsGeometry()
    {
        var offset = new Vector2(-479, 137);
        var first = JournalPopupLayout.Fit(new(400, 100), new(1080, 700), new(620, 370), 1);
        var moved = JournalPopupLayout.Fit(new Vector2(400, 100) + offset, new(1080, 700), new(620, 370), 1);
        Assert.Equal(first.Position + offset, moved.Position); Assert.Equal(first.Size, moved.Size);
    }
    [Theory]
    [InlineData(float.NaN, 100)] [InlineData(100, float.PositiveInfinity)] [InlineData(0, 100)]
    public void InvalidNativeContentCannotProduceAnOffscreenPopup(float width, float height) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => JournalPopupLayout.Fit(Vector2.Zero, new(width, height), new(620, 370), 1));
}
