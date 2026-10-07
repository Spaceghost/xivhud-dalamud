using XivHud.Journal;

public sealed class JournalCommandPolicyTests
{
    [Fact]
    public void ReadyJournalRequestCanToggleBookEvenWithHiddenCachedNativeAddon()
    {
        Assert.True(JournalCommandPolicy.CanReplace(4, true, true, true, false, false, true, true));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(3u)]
    [InlineData(5u)]
    [InlineData(uint.MaxValue)]
    public void EveryOtherMenuCommandRemainsNative(uint command)
    {
        Assert.False(JournalCommandPolicy.CanReplace(command, true, true, true, false, false, true, true));
    }

    [Fact]
    public void ExplicitNativeWindowHistoryLevesAndUnavailableBookStayNative()
    {
        Assert.False(JournalCommandPolicy.CanReplace(4, true, true, true, true, false, true, true));
        Assert.False(JournalCommandPolicy.CanReplace(4, true, true, true, false, true, true, true));
        Assert.False(JournalCommandPolicy.CanReplace(4, true, true, true, false, false, false, true));
        Assert.False(JournalCommandPolicy.CanReplace(4, true, true, false, false, false, true, true));
        Assert.False(JournalCommandPolicy.CanReplace(4, true, false, true, false, false, true, true));
        Assert.False(JournalCommandPolicy.CanReplace(4, false, true, true, false, false, true, true));
        Assert.False(JournalCommandPolicy.CanReplace(4, true, true, true, false, false, true, false));
    }

    [Fact]
    public void HiddenNativeAgentDoesNotKeepFallbackLeaseAfterWindowClosedOrOpeningExpired()
    {
        Assert.True(JournalCommandPolicy.KeepNativeBypass(false, false, false));
        Assert.True(JournalCommandPolicy.KeepNativeBypass(true, true, true));
        Assert.False(JournalCommandPolicy.KeepNativeBypass(false, true, false));
        Assert.False(JournalCommandPolicy.KeepNativeBypass(false, false, true));
    }
}
