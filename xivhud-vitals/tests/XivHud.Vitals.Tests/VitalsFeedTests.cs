using XivHud.Vitals.Core;

public sealed class VitalsFeedTests
{
    [Fact]
    public void PollsAtMostTwentyTimesASecondAndNeverUsesPreviewAsLiveFallback()
    {
        var reads = 0;
        var feed = new VitalsFeed(_ => { reads++; return null; });
        for (var now = 0; now < 1000; now++) Assert.Null(feed.Update(now, FeedAvailability.Ready));
        Assert.Equal(20, reads); Assert.Null(feed.Snapshot); Assert.Equal("Waiting for player", feed.Status);
    }

    [Theory]
    [InlineData(FeedAvailability.Disabled)]
    [InlineData(FeedAvailability.LoggedOut)]
    [InlineData(FeedAvailability.Hidden)]
    public void HiddenOrUnavailableStatesImmediatelyDiscardReadingsWithoutReadingAgain(FeedAvailability availability)
    {
        var reads = 0;
        var feed = new VitalsFeed(now => { reads++; return VitalsPreview.Create(now); });
        feed.Update(1000, FeedAvailability.Ready); Assert.NotNull(feed.Snapshot);
        feed.Update(1001, availability); Assert.Null(feed.Snapshot); Assert.Equal(1, reads);
        feed.Update(1002, FeedAvailability.Ready); Assert.NotNull(feed.Snapshot); Assert.Equal(2, reads);
        Assert.Equal(1002, feed.Snapshot!.ObservedAt);
    }

    [Fact]
    public void ReadFailureInvalidatesTheLastSnapshotAndBacksOffWithoutRepeatedLogErrors()
    {
        var reads = 0;
        var broken = false;
        var feed = new VitalsFeed(now => { reads++; return broken ? throw new InvalidOperationException("unavailable") : VitalsPreview.Create(now); });
        Assert.Null(feed.Update(0, FeedAvailability.Ready)); Assert.NotNull(feed.Snapshot);
        broken = true;
        Assert.IsType<InvalidOperationException>(feed.Update(50, FeedAvailability.Ready)); Assert.Null(feed.Snapshot);
        for (var i = 51; i < 1050; i++) Assert.Null(feed.Update(i, FeedAvailability.Ready));
        Assert.Equal(2, reads);
        Assert.Null(feed.Update(1050, FeedAvailability.Ready)); Assert.Equal(3, reads); Assert.Null(feed.Snapshot);
        broken = false;
        feed.Update(2050, FeedAvailability.Ready); Assert.NotNull(feed.Snapshot);
        broken = true;
        Assert.IsType<InvalidOperationException>(feed.Update(2100, FeedAvailability.Ready));
    }

    [Fact]
    public void LogoutAndExplicitResetCannotKeepAFailedReadCooldownOrOldPlayer()
    {
        var broken = true;
        var feed = new VitalsFeed(now => broken ? throw new Exception() : VitalsPreview.Create(now));
        Assert.NotNull(feed.Update(10, FeedAvailability.Ready));
        feed.Update(11, FeedAvailability.LoggedOut);
        broken = false;
        feed.Update(12, FeedAvailability.Ready); Assert.Equal(12, feed.Snapshot!.ObservedAt);
        feed.Reset(); Assert.Null(feed.Snapshot);
        feed.Update(13, FeedAvailability.Ready); Assert.Equal(13, feed.Snapshot!.ObservedAt);
    }
}
