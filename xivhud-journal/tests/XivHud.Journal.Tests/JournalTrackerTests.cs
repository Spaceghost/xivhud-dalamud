using System.Numerics;
using XivHud.Journal;

public class JournalTrackerTests
{
    private static JournalEntry Entry(uint id, bool tracked = true, bool priority = false, bool hidden = false)
        => new(id, "Quest " + id, 10, "Chapter", "Genre", tracked, priority, 1, ["Current step"], "", 0, hidden);

    [Fact]
    public void TrackedModeHonoursNativeVisibilityAndPrioritisesTheImportantQuest()
    {
        var entries = new[] { Entry(1), Entry(2, priority: true), Entry(3, tracked: false), Entry(4, hidden: true) };
        Assert.Equal(new uint[] { 2, 1 }, JournalTrackerPolicy.Select(entries, new HashSet<uint> { 3, 4 }, false, 3).Select(q => q.Id));
    }

    [Fact]
    public void BookmarksRemainLocalAndDoNotRequireOrChangeNativeTracking()
    {
        var entries = new[] { Entry(1), Entry(2, tracked: false), Entry(3, hidden: true) };
        Assert.Equal(new uint[] { 2 }, JournalTrackerPolicy.Select(entries, new HashSet<uint> { 2, 3, 999 }, true, 3).Select(q => q.Id));
        Assert.False(entries[1].Tracked);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 1)]
    [InlineData(3, 3)]
    [InlineData(100, 5)]
    public void SavedLimitsStayBounded(int requested, int expected)
        => Assert.Equal(expected, JournalTrackerPolicy.Select(Enumerable.Range(1, 8).Select(i => Entry((uint)i)), new HashSet<uint>(), false, requested).Length);

    [Fact]
    public void UnknownStepsDoNotInventFutureStoryAndCompletionIsExplicit()
    {
        Assert.Equal("Open Journal for the current objective.", JournalTrackerPolicy.Objective(Entry(1) with { Objectives = [] }));
        Assert.Equal("Ready to complete", JournalTrackerPolicy.Objective(Entry(1) with { ReadyToComplete = true, Objectives = ["Stale step"] }));
        Assert.Equal("Current step", JournalTrackerPolicy.Objective(Entry(1)));
    }

    [Fact]
    public void BadSavedAnchorsRecoverAndViewportResizeKeepsThePanelReachable()
    {
        Assert.Equal(new Vector2(.98f, .58f), JournalTrackerPolicy.Anchor(new(float.NaN)));
        Assert.Equal(new Vector2(0, 1), JournalTrackerPolicy.Anchor(new(-5, 6)));
        var origin = new Vector2(-1920, 48); var viewport = new Vector2(1920, 1080); var size = new Vector2(356, 400);
        var anchor = new Vector2(.35f, .7f);
        var p = JournalTrackerPolicy.Position(origin, viewport, size, anchor);
        var restored = JournalTrackerPolicy.FromPosition(p, origin, viewport, size);
        Assert.Equal(anchor.X, restored.X, 5); Assert.Equal(anchor.Y, restored.Y, 5);
        Assert.Equal(origin, JournalTrackerPolicy.Position(origin, new(100), size, Vector2.One));
    }
}
