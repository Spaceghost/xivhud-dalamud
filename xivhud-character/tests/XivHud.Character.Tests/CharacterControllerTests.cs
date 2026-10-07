using XivHud.Character;

public sealed class CharacterControllerTests
{
    private static CharacterPadFrame Frame(ulong sequence, uint held = 0, uint pressed = 0, string phase = "Active", float x = 0, float y = 0, float scroll = 0) => new(1, phase, sequence, held, pressed, 0, x, y, 0, scroll);

    [Fact] public void OpeningPressCannotActivateBeforeNeutral()
    {
        var input = new CharacterControllerInput();
        Assert.Empty(input.Read(Frame(1, 32, 32, "WaitingForNeutral"), 1, false));
        Assert.Empty(input.Read(Frame(2), 1.1, false));
        Assert.Equal(CharacterControllerCommand.Confirm, Assert.Single(input.Read(Frame(3, 32, 32), 1.2, false)).Command);
        Assert.Empty(input.Read(Frame(3, 32, 32), 1.3, false));
        Assert.Empty(input.Read(Frame(4, 32), 1.4, false));
    }
    [Fact] public void HeldDirectionRepeatsOnlyOnFreshFramesAndWithinBoundedRate()
    {
        var input = new CharacterControllerInput();
        Assert.Equal(CharacterControllerCommand.Down, Assert.Single(input.Read(Frame(1, 2, 2), 1, false)).Command);
        Assert.Empty(input.Read(Frame(2, 2), 1.1, false));
        Assert.Single(input.Read(Frame(3, 2), 1.36, false));
        Assert.Empty(input.Read(Frame(3, 2), 99, false));
        Assert.Single(input.Read(Frame(4, 2), 100, false));
        Assert.Empty(input.Read(Frame(5), 100.01, false));
        Assert.Single(input.Read(Frame(6, 2, 2), 100.02, false));
    }
    [Fact] public void HorizontalHoldRepeatsForColumnAndPageJumps()
    {
        var input = new CharacterControllerInput();
        Assert.Equal(CharacterControllerCommand.Right, Assert.Single(input.Read(Frame(1, 8, 8), 1, false)).Command);
        Assert.Empty(input.Read(Frame(2, 8), 1.2, false));
        Assert.Equal(CharacterControllerCommand.Right, Assert.Single(input.Read(Frame(3, 8), 1.4, false)).Command);
        Assert.Equal(CharacterControllerCommand.Left, Assert.Single(input.Read(Frame(4, 4), 1.41, false)).Command);
    }
    [Fact] public void AQuickReleasedPressIsNotLostBetweenFrames()
    {
        var input = new CharacterControllerInput();
        Assert.Equal(CharacterControllerCommand.Up, Assert.Single(input.Read(Frame(1, pressed: 1), 1, false)).Command);
        Assert.Empty(input.Read(Frame(2), 1.5, false));
    }
    [Theory]
    [InlineData(64u, CharacterControllerCommand.Secondary)] [InlineData(16u, CharacterControllerCommand.NativeDetails)]
    [InlineData(256u, CharacterControllerCommand.PreviousTab)] [InlineData(2048u, CharacterControllerCommand.NextTab)]
    [InlineData(32768u, CharacterControllerCommand.Appearance)] [InlineData(16384u, CharacterControllerCommand.Armoury)]
    [InlineData(32u, CharacterControllerCommand.Confirm)] [InlineData(128u, CharacterControllerCommand.Back)]
    public void PhysicalButtonsMatchRequestedActions(uint mask, CharacterControllerCommand expected) =>
        Assert.Equal(expected, Assert.Single(new CharacterControllerInput().Read(Frame(1, mask, mask), 1, false)).Command);
    [Fact] public void BackWinsSimultaneousConfirmAndPopupRequests()
    {
        var result = new CharacterControllerInput().Read(Frame(1, pressed: 128 | 32 | 32768 | 16384), 1, false);
        Assert.Equal(CharacterControllerCommand.Back, Assert.Single(result).Command);
    }
    [Fact] public void AppearanceWinsOverSimultaneousArmouryAndConfirm()
    {
        var result = new CharacterControllerInput().Read(Frame(1, pressed: 32768 | 16384 | 32), 1, false);
        Assert.Equal(CharacterControllerCommand.Appearance, Assert.Single(result).Command);
    }
    [Fact] public void SwappedConfirmBackAlsoChangesPlayStationLabels()
    {
        var result = new CharacterControllerInput().Read(Frame(1, pressed: 128), 1, true);
        Assert.Equal(CharacterControllerCommand.Confirm, Assert.Single(result).Command);
        var labels = CharacterControllerLabels.For("PlayStation", true);
        Assert.Equal("○", labels.Confirm); Assert.Equal("×", labels.Back);
    }
    [Theory]
    [InlineData("PlayStation", "□", "△", "Options", "Create")]
    [InlineData("Xbox", "X", "Y", "Menu", "View")]
    [InlineData("Generic", "West", "North", "Start", "Select")]
    [InlineData("Unknown", "□", "△", "Options", "Create")]
    public void LabelStylesNameTheArmouryAndOriginalWindowButtons(string style, string secondary, string details, string appearance, string armoury)
    {
        var labels = CharacterControllerLabels.For(style, false);
        Assert.Equal(secondary, labels.Secondary); Assert.Equal(details, labels.Details);
        Assert.Equal(appearance, labels.Appearance); Assert.Equal(armoury, labels.Armoury);
    }
    [Fact] public void AnalogDirectionsUseNegativeUpAndDominantAxis()
    {
        var input = new CharacterControllerInput();
        Assert.Empty(input.Read(Frame(1, x: .4f, y: .4f), 1, false));
        Assert.Equal(CharacterControllerCommand.Up, Assert.Single(input.Read(Frame(2, x: .6f, y: -.8f), 1.1, false)).Command);
        Assert.Equal(CharacterControllerCommand.Right, Assert.Single(input.Read(Frame(3, x: .9f, y: -.2f), 1.2, false)).Command);
    }
    [Fact] public void ScrollRejectsNonfiniteAndDoesNotJumpAfterStall()
    {
        var input = new CharacterControllerInput(); input.Read(Frame(1), 1, false);
        var scroll = Assert.Single(input.Read(Frame(2, scroll: 1), 20, false));
        Assert.Equal(CharacterControllerCommand.Scroll, scroll.Command); Assert.InRange(scroll.Amount, 0, 17.001f);
        Assert.Empty(input.Read(Frame(3, scroll: float.NaN), 20.1, false));
    }
    [Fact] public void FocusReleaseResetDoesNotRetainRepeatOrSequence()
    {
        var input = new CharacterControllerInput(); input.Read(Frame(100, 1), 1, false); input.Reset();
        Assert.Empty(input.Read(Frame(1, 1, phase: "Draining"), 2, false));
        Assert.Equal(CharacterControllerCommand.Down, Assert.Single(input.Read(Frame(2, 2), 2.1, false)).Command);
    }
    [Fact] public void ReloadedBrokerSessionCanRestartSequenceWithoutLosingControls()
    {
        var input = new CharacterControllerInput();
        input.Read(Frame(500, 1) with { Session = 9 }, 1, false);
        var result = input.Read(Frame(1, pressed: 64) with { Session = 10 }, 2, false);
        Assert.Equal(CharacterControllerCommand.Secondary, Assert.Single(result).Command);
    }
    [Fact] public void ReleasedAndRepressedDirectionBetweenFramesMovesAgain()
    {
        var input = new CharacterControllerInput();
        input.Read(Frame(1, 2, 2), 1, false);
        Assert.Equal(CharacterControllerCommand.Down, Assert.Single(input.Read(Frame(2, 2, 2), 1.1, false)).Command);
    }
    [Fact] public void UnknownFrameVersionProducesNothing() =>
        Assert.Empty(new CharacterControllerInput().Read(new CharacterPadFrame(2, "Active", 1, 32, 32, 0, 0, 0, 0, 0), 1, false));
}
