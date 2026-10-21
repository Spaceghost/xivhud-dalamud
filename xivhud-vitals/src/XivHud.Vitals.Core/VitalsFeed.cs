namespace XivHud.Vitals.Core;

public enum FeedAvailability { Disabled, LoggedOut, Hidden, Ready }

/// <summary>Owner-thread polling with immediate invalidation and bounded failure retries.</summary>
public sealed class VitalsFeed(Func<long, VitalsSnapshot?> read)
{
    private long nextRead;
    private bool failed;
    public VitalsSnapshot? Snapshot { get; private set; }
    public string Status { get; private set; } = "Disabled";

    public void Reset() { Snapshot = null; nextRead = 0; failed = false; }

    /// <returns>The first exception of a failed-read episode, for bounded owner-side logging.</returns>
    public Exception? Update(long now, FeedAvailability availability)
    {
        if (availability != FeedAvailability.Ready)
        {
            Reset();
            Status = availability switch
            {
                FeedAvailability.Disabled => "Disabled",
                FeedAvailability.LoggedOut => "Logged out",
                _ => "Hidden during UI hide, transition or cutscene",
            };
            return null;
        }
        if (now < nextRead) return null;
        nextRead = now + 50;
        try
        {
            Snapshot = read(now);
            Status = Snapshot is null ? "Waiting for player" : "Live";
            failed = false;
        }
        catch (Exception error)
        {
            Snapshot = null;
            Status = "Unavailable · native HUD remains available";
            nextRead = now + 1000;
            if (!failed) { failed = true; return error; }
        }
        return null;
    }
}
