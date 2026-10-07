using XivHud.Journal;

public sealed class JournalTextTests
{
    [Fact]
    public void CurrentStepDoesNotRevealPastFutureOrUnusedObjectives()
    {
        JournalTodo[] todos = [new(0, "Unused spoiler"), new(1, "Past step"),
            new(2, " Speak to the guildmaster. "), new(2, "Speak to the guildmaster."),
            new(2, ""), new(3, "Future reveal"), new(255, "Turn in")];
        Assert.Equal(new[] { "Speak to the guildmaster." }, JournalText.CurrentObjectives(todos, 2));
        Assert.Empty(JournalText.CurrentObjectives(todos, 0));
        Assert.Empty(JournalText.CurrentObjectives(todos, 4));
        Assert.Equal(new[] { "Turn in" }, JournalText.CurrentObjectives(todos, 255));
    }

    [Theory]
    [InlineData(1u, 1)]
    [InlineData(39u, 39)]
    [InlineData(65575u, 39)]
    [InlineData(131071u, 65535)]
    public void NativeIdAcceptsClientAndQuestSheetRanges(uint id, ushort expected)
    {
        Assert.True(JournalText.TryClientId(id, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(65536u)]
    [InlineData(131072u)]
    [InlineData(uint.MaxValue)]
    public void OtherSheetRangesDoNotAliasAQuest(uint id)
    {
        Assert.False(JournalText.TryClientId(id, out var actual));
        Assert.Equal((ushort)0, actual);
    }

    [Fact]
    public void OnlyStructuredQuestTextKeysResolve()
    {
        Assert.Equal("quest/000/SubFst010_00039", JournalText.SheetPath("SubFst010_00039"));
        Assert.Null(JournalText.SheetPath("missing_separator"));
        Assert.Null(JournalText.SheetPath("SubFst010_0003"));
        Assert.Null(JournalText.SheetPath("SubFst010_00a39"));
        Assert.Null(JournalText.SheetPath("../SubFst010_00039"));
        Assert.Null(JournalText.SheetPath("..\\SubFst010_00039"));
    }

    [Fact]
    public void NarrativeAndOtherSuffixesCannotBecomeObjectives()
    {
        Assert.Equal(2, JournalText.TodoIndex("TEXT_SUBFST010_00039_TODO_02"));
        Assert.Null(JournalText.TodoIndex("TEXT_SUBFST010_00039_SEQ_02"));
        Assert.Null(JournalText.TodoIndex("TEXT_SUBFST010_00039_TODO_02_EXTRA"));
        Assert.Null(JournalText.TodoIndex("TEXT_SUBFST010_00039_TODO_-1"));
        Assert.Null(JournalText.TodoIndex("TEXT_SUBFST010_00039_TODO_1000"));
    }
}
