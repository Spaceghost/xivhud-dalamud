using XivHud.Journal;

public sealed class ControllerTests
{
    private static JournalPadFrame Frame(ulong sequence, uint held = 0, uint pressed = 0, string phase = "Active", float x = 0, float y = 0, float scroll = 0) => new(1, phase, sequence, held, pressed, 0, x, y, 0, scroll);

    [Fact] public void OpeningPressCannotActivateQuestBeforeNeutral()
    {
        var input = new JournalControllerInput();
        Assert.Empty(input.Read(Frame(1, 32, 32, "WaitingForNeutral"), 1, false));
        Assert.Empty(input.Read(Frame(2), 1.1, false));
        Assert.Equal(JournalControllerCommand.Confirm, Assert.Single(input.Read(Frame(3, 32, 32), 1.2, false)).Command);
        Assert.Empty(input.Read(Frame(3, 32, 32), 1.3, false));
        Assert.Empty(input.Read(Frame(4, 32), 1.4, false));
    }
    [Fact] public void HeldDirectionRepeatsOnlyOnFreshFramesAndWithinBoundedRate()
    {
        var input = new JournalControllerInput();
        Assert.Equal(JournalControllerCommand.Down, Assert.Single(input.Read(Frame(1, 2, 2), 1, false)).Command);
        Assert.Empty(input.Read(Frame(2, 2), 1.1, false));
        Assert.Single(input.Read(Frame(3, 2), 1.36, false));
        Assert.Empty(input.Read(Frame(3, 2), 99, false));
        Assert.Single(input.Read(Frame(4, 2), 100, false));
        Assert.Empty(input.Read(Frame(5), 100.01, false));
        Assert.Single(input.Read(Frame(6, 2, 2), 100.02, false));
    }
    [Fact] public void AQuickReleasedPressIsNotLostBetweenFrames()
    {
        var input = new JournalControllerInput();
        Assert.Equal(JournalControllerCommand.Up, Assert.Single(input.Read(Frame(1, pressed: 1), 1, false)).Command);
        Assert.Empty(input.Read(Frame(2), 1.5, false));
    }
    [Theory]
    [InlineData(64u, JournalControllerCommand.Bookmark)] [InlineData(16u, JournalControllerCommand.NativeDetails)]
    [InlineData(256u, JournalControllerCommand.PreviousTab)] [InlineData(2048u, JournalControllerCommand.NextTab)]
    [InlineData(32768u, JournalControllerCommand.Appearance)] [InlineData(16384u, JournalControllerCommand.Search)]
    public void PhysicalButtonsMatchRequestedActions(uint mask, JournalControllerCommand expected) =>
        Assert.Equal(expected, Assert.Single(new JournalControllerInput().Read(Frame(1, mask, mask), 1, false)).Command);
    [Fact] public void BackWinsSimultaneousConfirmAndPopupRequests()
    {
        var result = new JournalControllerInput().Read(Frame(1, pressed: 128 | 32 | 32768), 1, false);
        Assert.Equal(JournalControllerCommand.Back, Assert.Single(result).Command);
    }
    [Fact] public void SwappedConfirmBackAlsoChangesPlayStationLabels()
    {
        var result = new JournalControllerInput().Read(Frame(1, pressed: 128), 1, true);
        Assert.Equal(JournalControllerCommand.Confirm, Assert.Single(result).Command);
        var labels = JournalControllerLabels.For("PlayStation", true);
        Assert.Equal("○", labels.Confirm); Assert.Equal("×", labels.Back);
    }
    [Fact] public void AnalogDirectionsUseNegativeUpAndDominantAxis()
    {
        var input = new JournalControllerInput();
        Assert.Empty(input.Read(Frame(1, x: .4f, y: .4f), 1, false));
        Assert.Equal(JournalControllerCommand.Up, Assert.Single(input.Read(Frame(2, x: .6f, y: -.8f), 1.1, false)).Command);
        Assert.Equal(JournalControllerCommand.Right, Assert.Single(input.Read(Frame(3, x: .9f, y: -.2f), 1.2, false)).Command);
    }
    [Fact] public void ScrollRejectsNonfiniteAndDoesNotJumpAfterStall()
    {
        var input = new JournalControllerInput(); input.Read(Frame(1), 1, false);
        var scroll = Assert.Single(input.Read(Frame(2, scroll: 1), 20, false));
        Assert.Equal(JournalControllerCommand.Scroll, scroll.Command); Assert.InRange(scroll.Amount, 0, 17.001f);
        Assert.Empty(input.Read(Frame(3, scroll: float.NaN), 20.1, false));
    }
    [Fact] public void FocusReleaseResetDoesNotRetainRepeatOrSequence()
    {
        var input = new JournalControllerInput(); input.Read(Frame(100, 1), 1, false); input.Reset();
        Assert.Empty(input.Read(Frame(1, 1, phase: "Draining"), 2, false));
        Assert.Equal(JournalControllerCommand.Down, Assert.Single(input.Read(Frame(2, 2), 2.1, false)).Command);
    }
    [Fact] public void ReloadedBrokerSessionCanRestartSequenceWithoutLosingControls()
    {
        var input = new JournalControllerInput();
        input.Read(Frame(500, 1) with { Session = 9 }, 1, false);
        var result = input.Read(Frame(1, pressed: 64) with { Session = 10 }, 2, false);
        Assert.Equal(JournalControllerCommand.Bookmark, Assert.Single(result).Command);
    }
    [Fact] public void ReleasedAndRepressedDirectionBetweenFramesMovesAgain()
    {
        var input = new JournalControllerInput();
        input.Read(Frame(1, 2, 2), 1, false);
        Assert.Equal(JournalControllerCommand.Down, Assert.Single(input.Read(Frame(2, 2, 2), 1.1, false)).Command);
    }
    [Fact] public void KeyboardActionsEditRealGraphemesAndRespectLength()
    {
        Assert.Equal("Quest ", JournalSearchKeyboard.Edit("Quest", "Space"));
        Assert.Equal("a", JournalSearchKeyboard.Edit("a👩‍🚀", "Backspace"));
        Assert.Equal("a", JournalSearchKeyboard.Edit("ae\u0301", "Backspace"));
        Assert.Equal("", JournalSearchKeyboard.Edit("", "Backspace"));
        Assert.Equal("", JournalSearchKeyboard.Edit("abc", "Clear"));
        var full = new string('a', 160); Assert.Equal(full, JournalSearchKeyboard.Edit(full, "x"));
    }
    [Fact] public void KeyboardRowsAndSymbolPageStayWithinVisibleKeys()
    {
        var keyboard = new JournalSearchKeyboard(); keyboard.Move(99, 0); keyboard.Move(0, 3);
        Assert.Equal("Done", keyboard.Selected); keyboard.Move(0, -1); Assert.Equal(".", keyboard.Selected);
        keyboard.ChangePage(-1); Assert.Equal("ß", keyboard.Selected);
        keyboard.Move(-99, -99); Assert.Equal("1", keyboard.Selected);
    }
}
