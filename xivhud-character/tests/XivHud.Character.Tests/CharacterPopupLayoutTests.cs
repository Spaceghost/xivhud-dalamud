using System.Numerics;
using XivHud.Character;

public sealed class CharacterPopupLayoutTests
{
    [Theory]
    [InlineData(1120, 720, 1, 470, 650)]
    [InlineData(960, 640, 1, 470, 650)]
    [InlineData(768, 512, .8f, 470, 650)]
    [InlineData(2240, 1440, 2, 470, 650)]
    [InlineData(32, 32, .05f, 470, 650)]
    public void AppearanceRemainsInsideCapturedContent(float width, float height, float scale, float pw, float ph)
    {
        var origin = new Vector2(351, 192); var size = new Vector2(width, height);
        var popup = CharacterPopupLayout.Fit(origin, size, new(pw * scale, ph * scale), scale);
        Assert.True(popup.Size.X > 0 && popup.Size.Y > 0);
        Assert.True(popup.Position.X > origin.X && popup.Position.Y > origin.Y);
        Assert.True(popup.Position.X + popup.Size.X < origin.X + size.X);
        Assert.True(popup.Position.Y + popup.Size.Y < origin.Y + size.Y);
        Assert.Equal(origin + size / 2, popup.Position + popup.Size / 2);
    }
    [Fact] public void MinimumRecordClampsTheTallAppearancePopupButKeepsItsWidth()
    {
        var popup = CharacterPopupLayout.Fit(Vector2.Zero, new(960, 640), new(470, 650), 1);
        Assert.Equal(470, popup.Size.X); Assert.Equal(640 - 36, popup.Size.Y);
    }
    [Fact] public void MovingNativeFrameMovesPopupWithoutChangingItsGeometry()
    {
        var offset = new Vector2(-479, 137);
        var first = CharacterPopupLayout.Fit(new(400, 100), new(1120, 720), new(470, 650), 1);
        var moved = CharacterPopupLayout.Fit(new Vector2(400, 100) + offset, new(1120, 720), new(470, 650), 1);
        Assert.Equal(first.Position + offset, moved.Position); Assert.Equal(first.Size, moved.Size);
    }
    [Theory]
    [InlineData(float.NaN, 100)] [InlineData(100, float.PositiveInfinity)] [InlineData(0, 100)]
    public void InvalidNativeContentCannotProduceAnOffscreenPopup(float width, float height) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => CharacterPopupLayout.Fit(Vector2.Zero, new(width, height), new(470, 650), 1));
}
