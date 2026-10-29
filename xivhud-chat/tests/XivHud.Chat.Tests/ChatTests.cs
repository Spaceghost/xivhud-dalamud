using XivHud.Chat.Core;

namespace XivHud.Chat.Tests;

public sealed class ChatTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-28T20:00:00Z");
    [Fact]
    public void HistoryIsBoundedAndUnreadCountsOnlyRetainedMessages()
    {
        var store = new ChatStore(3);
        for (var i = 0; i < 10; i++) store.Append(Now, ChatChannel.Party, "Companion", "line " + i);
        Assert.Equal(3, store.Snapshot().Count); Assert.Equal(3, store.Unread(ChatChannel.All));
        Assert.Equal("line 7", store.Snapshot()[0].Text);
        store.MarkRead(ChatChannel.Party, store.Snapshot().Last().Id); Assert.Equal(0, store.Unread(ChatChannel.All));
    }
    [Fact]
    public void MarkReadDoesNotConsumeMessagesArrivingAfterTheSnapshot()
    {
        var store = new ChatStore(); var first = store.Append(Now, ChatChannel.Tell, "Friend", "hello")!;
        store.Append(Now, ChatChannel.Tell, "Friend", "second"); store.MarkRead(ChatChannel.Tell, first.Id);
        Assert.Equal(1, store.Unread(ChatChannel.Tell));
        store.MarkRead(ChatChannel.Party, long.MaxValue); Assert.Equal(1, store.Unread(ChatChannel.Tell));
    }
    [Fact]
    public void SearchAndChannelsAreIndependent()
    {
        var store = new ChatStore(); store.Append(Now, ChatChannel.Party, "Builder", "Ready"); store.Append(Now, ChatChannel.Tell, "Friend", "Ready too");
        Assert.Single(store.Snapshot(ChatChannel.Party, "READY")); Assert.Single(store.Snapshot(ChatChannel.All, "friend"));
    }
    [Fact]
    public void LogoutClearRemovesAllSessionContentAndKeepsIdsMonotonic()
    {
        var store = new ChatStore(); var old = store.Append(Now, ChatChannel.Say, "Sample", "old")!;
        store.Clear(); Assert.Empty(store.Snapshot()); Assert.Equal(0, store.Unread(ChatChannel.All));
        Assert.True(store.Append(Now, ChatChannel.Say, "Sample", "new")!.Id > old.Id);
    }
    [Theory]
    [InlineData("Damage", true)] [InlineData("GainBuff", true)] [InlineData("Action", true)] [InlineData("Say", false)] [InlineData("TellIncoming", false)]
    public void CombatLogSpamIsSeparateFromConversations(string type, bool ignored) => Assert.Equal(ignored, ChatStore.IsCombatLog(type));
    [Theory]
    [InlineData("TellIncoming", ChatChannel.Tell)] [InlineData("GmTell", ChatChannel.Tell)] [InlineData("CrossParty", ChatChannel.Party)] [InlineData("SystemError", ChatChannel.System)] [InlineData("Ls1", ChatChannel.Linkshell)]
    public void NativeChannelClassificationIsStable(string value, ChatChannel channel) => Assert.Equal(channel, ChatStore.Classify(value));
    [Fact]
    public void TextIsBoundedAndUnicodeRemainsWhole()
    {
        Assert.Equal("😀", ChatStore.Clean("😀x", 1)); Assert.Equal("a b", ChatStore.Clean("a\nb", 10));
        var store = new ChatStore(); var entry = store.Append(Now, ChatChannel.Say, new string('s', 500), new string('x', 5000))!;
        Assert.Equal(120, entry.Sender.Length); Assert.Equal(2000, entry.Text.Length);
    }
    [Theory]
    [InlineData(ChatChannel.Say, "/s hi")] [InlineData(ChatChannel.Party, "/p hi")] [InlineData(ChatChannel.Alliance, "/a hi")] [InlineData(ChatChannel.FreeCompany, "/fc hi")]
    public void ComposerBuildsOnlyAnExplicitChannel(ChatChannel channel, string expected)
    { Assert.Equal(expected, ChatComposer.Build(channel, "hi", "", out var error)); Assert.Null(error); }
    [Theory]
    [InlineData("")] [InlineData("/wave")] [InlineData(" /wave")] [InlineData("a\nb")] [InlineData("a\rb")] [InlineData("a\0b")]
    public void BlankCommandsAndMultipleLinesAreRejected(string body) => Assert.Null(ChatComposer.Build(ChatChannel.Say, body, "", out _));
    [Fact]
    public void Utf8LimitIncludesTheChannelPrefix()
    {
        Assert.NotNull(ChatComposer.Build(ChatChannel.Say, new string('a', 497), "", out _));
        Assert.Null(ChatComposer.Build(ChatChannel.Say, new string('a', 498), "", out _));
        Assert.Null(ChatComposer.Build(ChatChannel.Say, string.Concat(Enumerable.Repeat("猫", 166)), "", out _));
    }
    [Theory]
    [InlineData("Friend")] [InlineData("First Last")] [InlineData("First Last@World@Elsewhere")] [InlineData("First Last@World /p")]
    public void TellRecipientMustNameOneCharacterAndWorld(string recipient) => Assert.Null(ChatComposer.Build(ChatChannel.Tell, "hi", recipient, out _));
    [Fact]
    public void TellRecipientIsNormalizedAndStillRequiresExplicitSubmission()
    {
        var line = ChatComposer.Build(ChatChannel.Tell, "hi", "First  Last@World", out _);
        Assert.Equal("/tell First Last@World hi", line);
        Assert.True(ChatComposer.CanSubmit(new(12, line!, 1000), 12, 1200, true, true, false));
    }
    [Theory]
    [InlineData(13, 1200, true, true, false)] [InlineData(12, 2001, true, true, false)] [InlineData(12, 999, true, true, false)]
    [InlineData(12, 1200, false, true, false)] [InlineData(12, 1200, true, false, false)] [InlineData(12, 1200, true, true, true)]
    public void NativeSubmissionRequiresSamePlayerFreshnessAndExplicitLiveMode(ulong player, long now, bool enabled, bool allowed, bool preview)
        => Assert.False(ChatComposer.CanSubmit(new(12, "/s hi", 1000), player, now, enabled, allowed, preview));
    [Fact]
    public void ArbitraryNativeCommandsCannotEnterTheSendQueue()
    {
        foreach (var line in new[] { "/xivhud on", "/shutdown", "/s hi\n/p surprise", "/s /command" })
            Assert.False(ChatComposer.CanSubmit(new(12, line, 1000), 12, 1200, true, true, false));
    }
    [Fact]
    public void NotificationsDeduplicateAndKeepHistoryAfterExpiry()
    {
        var store = new NotificationStore(); store.Push("Game", "Error", "Not ready", NotificationKind.Error, 1000);
        store.Push("Game", "Error", "Not ready", NotificationKind.Error, 1100);
        var item = Assert.Single(store.Active(1200)); Assert.Equal(2, item.Count);
        Assert.Empty(store.Active(8100)); Assert.Single(store.History());
        store.Clear(); Assert.Empty(store.History());
    }
    [Fact]
    public void NotificationStackPrioritizesErrorsAndDoesNotGrowWithoutBound()
    {
        var store = new NotificationStore();
        for (var i = 0; i < 200; i++) store.Push("Game", "Notice", "entry " + i, NotificationKind.Info, 1000);
        store.Push("Game", "Failure", "important", NotificationKind.Error, 1000);
        Assert.Equal(64, store.Active(1000, 64).Count); Assert.Equal(128, store.History().Count);
        Assert.Equal(NotificationKind.Error, store.Active(1000)[0].Kind); Assert.Equal(3, store.Active(1000).Count);
    }
    [Fact]
    public void DismissAndClockRollbackCannotLeaveAnImmortalCard()
    {
        var store = new NotificationStore(); store.Push("Mod", "Title", "Body", NotificationKind.Message, 1000);
        var item = Assert.Single(store.Active(1100)); store.Dismiss(item.Id); Assert.Empty(store.Active(1200)); Assert.Single(store.History());
        store.Push("Mod", "Other", "Body", NotificationKind.Message, 2000); Assert.Empty(store.Active(1999));
    }
}
