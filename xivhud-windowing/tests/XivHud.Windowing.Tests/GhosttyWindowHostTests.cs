using System.Text.Json;
using XivHud.Windowing;

namespace XivHud.Windowing.Tests;

public sealed class GhosttyWindowHostTests
{
    private const string Name = "Adventurer's Journal###XivHud.Journal";

    [Fact]
    public void DisabledHostMakesNoCalls()
    {
        var transport = new FakeGhostty();
        using var host = Create(transport);
        host.Update(false, true, true, 0);
        Assert.Empty(transport.Calls);
        Assert.Empty(transport.Posts);
        Assert.Equal(WindowHostState.Screen, host.State);
        Assert.False(host.RequiresHostFocus);
        Assert.True(host.HasInputFocus);
        Assert.Empty(transport.Calls);
    }

    [Fact]
    public void MissingProviderLeavesNormalDrawAvailableAndRetriesWithoutQueuingAdoption()
    {
        var transport = new FakeGhostty { Available = false };
        using var host = Create(transport);
        host.Update(true, true, true, 0);
        Assert.Equal(WindowHostState.WaitingForHost, host.State);
        Assert.False(host.IsHosted);
        Assert.True(host.RequiresHostFocus);
        Assert.False(host.HasInputFocus);
        Assert.Empty(transport.Posts);
        transport.Available = true;
        host.Update(true, true, true, 1);
        Assert.Equal("adopt " + Name, Assert.Single(transport.Posts));
        Assert.False(host.HasInputFocus);
    }

    [Fact]
    public void HostedFocusIsReadEveryTimeEvenBetweenPlacementPolls()
    {
        var transport = new FakeGhostty();
        using var host = Create(transport);
        Host(host, transport);
        Assert.True(host.RequiresHostFocus);
        Assert.False(host.HasInputFocus);
        transport.FocusedId = host.PanelId;
        Assert.True(host.HasInputFocus); // Adopted panels use kind=terminal in the real provider.
        transport.FocusedId = 900;
        Assert.False(host.HasInputFocus);
        transport.FocusedId = host.PanelId;
        transport.FocusedKind = "window";
        Assert.True(host.HasInputFocus);
        transport.FocusedId = 0;
        Assert.False(host.HasInputFocus);
        Assert.Equal(5, transport.Calls.Count(c => c.Contains("\"method\":\"focus.get\"")));
        Assert.DoesNotContain(transport.Calls, c => c.Contains("focus.cycle") || c.Contains("panel.focus") || c.Contains("window.focus"));
        Assert.Single(transport.Calls, IsPlacement);
    }

    [Fact]
    public void ReusedOrMissingPanelIdentityCannotRetainFocusBetweenPlacementPolls()
    {
        var transport = new FakeGhostty();
        using var host = Create(transport);
        Host(host, transport);
        transport.FocusedId = host.PanelId;
        Assert.True(host.HasInputFocus);
        transport.Panels.Clear();
        Assert.False(host.HasInputFocus);
        transport.Panels.Add(new(host.PanelId, "adopted", "Other###Owner", "pet", "Journal"));
        Assert.False(host.HasInputFocus);
        transport.Panels.Add(new(host.PanelId + 1, "adopted", Name, "pet", "Journal"));
        Assert.False(host.HasInputFocus);
        Assert.Equal(WindowHostState.Hosted, host.State); // Read-only check does not update placement.
    }

    [Fact]
    public void ProviderLossImmediatelyBlocksHostedInputWithoutWaitingForStatePoll()
    {
        var transport = new FakeGhostty();
        using var host = Create(transport);
        Host(host, transport);
        transport.FocusedId = host.PanelId;
        Assert.True(host.HasInputFocus);
        transport.Available = false;
        Assert.False(host.HasInputFocus);
        Assert.True(host.RequiresHostFocus);
        Assert.Equal(WindowHostState.Hosted, host.State);
    }

    [Fact]
    public void ReturningAndDisposedHostsNeverGrantInputEvenWithMatchingHostFocus()
    {
        var transport = new FakeGhostty();
        var host = Create(transport);
        Host(host, transport);
        transport.FocusedId = host.PanelId;
        Assert.True(host.HasInputFocus);
        host.ReturnToScreen();
        var calls = transport.Calls.Count;
        Assert.True(host.RequiresHostFocus);
        Assert.False(host.HasInputFocus);
        Assert.Equal(calls, transport.Calls.Count);
        host.Update(true, true, true, 1);
        transport.Frame();
        host.Update(true, true, true, 1.3);
        Assert.Equal(WindowHostState.ReturnedToScreen, host.State);
        Assert.False(host.RequiresHostFocus);
        calls = transport.Calls.Count;
        Assert.True(host.HasInputFocus);
        Assert.Equal(calls, transport.Calls.Count);
        host.Dispose();
        Assert.True(host.RequiresHostFocus);
        Assert.False(host.HasInputFocus);
    }

    [Theory]
    [InlineData("{\"ok\":true,\"result\":{}}")]
    [InlineData("{\"ok\":true,\"result\":{\"id\":100,\"kind\":null}}")]
    [InlineData("{\"ok\":true,\"result\":{\"id\":100,\"kind\":\"unknown\"}}")]
    [InlineData("{\"ok\":true,\"result\":{\"id\":\"100\",\"kind\":\"terminal\"}}")]
    [InlineData("{\"ok\":false,\"error\":\"unavailable\"}")]
    [InlineData("invalid JSON")]
    public void MalformedHostFocusCannotGrantInput(string reply)
    {
        var transport = new FakeGhostty { FocusReply = reply };
        using var host = Create(transport);
        Host(host, transport);
        Assert.False(host.HasInputFocus);
    }

    [Fact]
    public void HudPlacementDoesNotRequestFocusWithoutExplicitUserIntent()
    {
        var transport = new FakeGhostty();
        using var host = Create(transport);
        HostHud(host, transport);
        host.Update(true, true, true, 1, WindowPlacement.Hud);
        Assert.DoesNotContain(transport.Calls, IsFocus);
        Assert.False(host.HasInputFocus);
    }

    [Fact]
    public void ExplicitFocusWaitsForHudPlacementAndActualFocusRemainsAuthoritative()
    {
        var transport = new FakeGhostty();
        using var host = Create(transport);
        host.RequestInputFocus();
        host.Update(true, true, true, 0, WindowPlacement.Hud);
        Assert.DoesNotContain(transport.Calls, IsFocus);
        transport.Frame();
        host.Update(true, true, true, .3, WindowPlacement.Hud);
        Assert.DoesNotContain(transport.Calls, IsFocus); // Placement queued but not acknowledged.
        host.RequestInputFocus(); // Coalesces the same pending opening intent.
        host.Update(true, true, true, .4, WindowPlacement.Hud);
        Assert.DoesNotContain(transport.Calls, IsFocus);
        transport.Frame();
        host.Update(true, true, true, .7, WindowPlacement.Hud);
        var request = Assert.Single(transport.Calls, IsFocus);
        using var json = JsonDocument.Parse(request);
        Assert.Equal(host.PanelId, json.RootElement.GetProperty("params").GetProperty("id").GetInt32());
        Assert.False(host.HasInputFocus); // Queue acknowledgement grants no input.
        host.RequestInputFocus(); // Also coalesces while the focus command is in flight.
        transport.Frame();
        host.Update(true, true, true, 1, WindowPlacement.Hud);
        Assert.True(host.HasInputFocus);
        transport.FocusedId = 0;
        host.Update(true, true, true, 2, WindowPlacement.Hud);
        Assert.False(host.HasInputFocus);
        Assert.Single(transport.Calls, IsFocus); // Losing focus never steals it back.
    }

    [Theory]
    [InlineData(WindowPlacement.Companion)]
    [InlineData(WindowPlacement.World)]
    [InlineData(WindowPlacement.Ground)]
    public void ExplicitFocusDoesNotTargetOtherWorldPlacements(WindowPlacement placement)
    {
        var transport = new FakeGhostty();
        using var host = Create(transport);
        host.RequestInputFocus();
        host.Update(true, true, true, 0, placement);
        transport.Frame();
        host.Update(true, true, true, .3, placement);
        transport.Frame();
        host.Update(true, true, true, .6, placement);
        Assert.DoesNotContain(transport.Calls, IsFocus);
        // Refused intent must not silently become a new focus request after a later move.
        host.Update(true, true, true, 1, WindowPlacement.Hud);
        transport.Frame();
        host.Update(true, true, true, 1.3, WindowPlacement.Hud);
        Assert.DoesNotContain(transport.Calls, IsFocus);
    }

    [Fact]
    public void PlacementAcknowledgementAloneCannotFocusAStillNonHudPanel()
    {
        var transport = new FakeGhostty { IgnorePlacementView = true };
        using var host = Create(transport);
        host.RequestInputFocus();
        HostHud(host, transport);
        Assert.DoesNotContain(transport.Calls, IsFocus);
        transport.Panels[0] = transport.Panels[0] with { View = "hud" };
        host.Update(true, true, true, 1, WindowPlacement.Hud);
        Assert.Single(transport.Calls, IsFocus);
    }

    [Fact]
    public void FailedPlacementCannotSendFocus()
    {
        var transport = new FakeGhostty { PlacementFailure = "placement refused" };
        using var host = Create(transport);
        host.RequestInputFocus();
        HostHud(host, transport);
        host.Update(true, true, true, 8, WindowPlacement.Hud);
        Assert.DoesNotContain(transport.Calls, IsFocus);
        Assert.Contains("placement refused", host.Status);
    }

    [Fact]
    public void ClosingBeforeConfirmedPlacementCancelsOpeningFocus()
    {
        var transport = new FakeGhostty();
        using var host = Create(transport);
        host.RequestInputFocus();
        host.Update(true, true, true, 0, WindowPlacement.Hud);
        transport.Frame();
        host.Update(true, true, true, .3, WindowPlacement.Hud);
        host.Update(true, false, true, .4, WindowPlacement.Hud);
        transport.Frame();
        host.Update(true, false, true, .7, WindowPlacement.Hud);
        host.Update(true, true, true, 1, WindowPlacement.Hud);
        transport.Frame();
        host.Update(true, true, true, 1.3, WindowPlacement.Hud);
        transport.Frame();
        host.Update(true, true, true, 1.6, WindowPlacement.Hud);
        Assert.DoesNotContain(transport.Calls, IsFocus);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedOrAmbiguousFocusIsReportedWithoutAutomaticRetry(bool ambiguousSend)
    {
        var transport = new FakeGhostty
        {
            FocusFailure = ambiguousSend ? null : "the panel is hidden",
            ThrowAfterFocusEnqueue = ambiguousSend,
        };
        using var host = Create(transport);
        host.RequestInputFocus();
        HostHud(host, transport);
        transport.Frame();
        host.Update(true, true, true, 1, WindowPlacement.Hud);
        host.Update(true, true, true, 10, WindowPlacement.Hud);
        Assert.Single(transport.Calls, IsFocus);
        Assert.Contains(ambiguousSend ? "focus acknowledgement lost" : "the panel is hidden", host.Status);
        if (!ambiguousSend) Assert.False(host.HasInputFocus);
    }

    [Fact]
    public void DisposalCancelsUnsentOpeningFocus()
    {
        var transport = new FakeGhostty();
        var host = Create(transport);
        host.RequestInputFocus();
        host.Dispose();
        host.RequestInputFocus();
        host.Update(true, true, true, 0, WindowPlacement.Hud);
        Assert.Empty(transport.Calls);
        Assert.Empty(transport.Posts);
    }

    [Fact]
    public void AdoptionHasToAppearInExactRegisteredIdentityBeforeItIsHosted()
    {
        var transport = new FakeGhostty();
        transport.Panels.Add(new(7, "adopted", "Other window###Other", "pet", "Adventurer's Journal"));
        transport.Panels.Add(new(8, "terminal", Name, "pet", "Adventurer's Journal"));
        using var host = Create(transport);
        host.Update(true, true, true, 0);
        Assert.False(host.IsHosted);
        Assert.Equal(0, host.PanelId);
        transport.Frame();
        host.Update(true, true, true, .3);
        Assert.True(host.IsHosted);
        Assert.Equal(100, host.PanelId);
        Assert.DoesNotContain(transport.Calls, c => c.Contains("panel.focus"));
    }

    [Theory]
    [InlineData(WindowPlacement.Companion, "pet")]
    [InlineData(WindowPlacement.Hud, "hud")]
    [InlineData(WindowPlacement.World, "here")]
    [InlineData(WindowPlacement.Ground, "ground")]
    public void PlacementTargetsOnlyOwnedPanelAndIsNotReappliedEveryPoll(WindowPlacement placement, string argument)
    {
        var transport = new FakeGhostty();
        using var host = Create(transport);
        host.Update(true, true, true, 0, placement);
        transport.Frame();
        host.Update(true, true, true, .3, placement);
        transport.Frame();
        host.Update(true, true, true, .6, placement);
        host.Update(true, true, true, .9, placement);
        var placementCall = Assert.Single(transport.Calls, IsPlacement);
        using var document = JsonDocument.Parse(placementCall);
        Assert.Equal(100, document.RootElement.GetProperty("params").GetProperty("id").GetInt32());
        Assert.Equal(argument, document.RootElement.GetProperty("params").GetProperty("pin").GetString());
    }

    [Fact]
    public void BackToUiIsRespectedUntilWindowReopens()
    {
        var transport = new FakeGhostty();
        using var host = Create(transport);
        Host(host, transport);
        transport.Panels.Clear();
        host.Update(true, true, true, 1);
        host.Update(true, true, true, 5);
        Assert.Equal(WindowHostState.ReturnedToScreen, host.State);
        Assert.Single(transport.Posts);
        host.Update(true, false, true, 6);
        host.Update(true, true, true, 7);
        Assert.Equal(2, transport.Posts.Count(p => p.StartsWith("adopt ")));
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void DisableCloseAndLogoutReleaseOnlyOwnedWindow(bool enabled, bool open, bool loggedIn)
    {
        var transport = new FakeGhostty();
        transport.Panels.Add(new(7, "adopted", "Another###Other", "pet", "Other"));
        using var host = Create(transport);
        Host(host, transport);
        host.Update(enabled, open, loggedIn, 1);
        Assert.Equal("release " + Name, transport.Posts.Last());
        transport.Frame();
        host.Update(enabled, open, loggedIn, 1.3);
        Assert.Equal(WindowHostState.Screen, host.State);
        Assert.Equal(7, Assert.Single(transport.Panels).Id);
    }

    [Fact]
    public void ClosingBeforeAdoptionIsProcessedCancelsThePendingCommandInOrder()
    {
        var transport = new FakeGhostty();
        using var host = Create(transport);
        host.Update(true, true, true, 0);
        host.Update(true, false, true, .1);
        Assert.Equal(new[] { "adopt " + Name, "release " + Name }, transport.Posts);
        transport.Frame();
        host.Update(true, false, true, .4);
        Assert.Empty(transport.Panels);
        Assert.False(host.IsHosted);
    }

    [Fact]
    public void ReleaseWaitsAcrossProviderAbsenceThenReleasesOnReturn()
    {
        var transport = new FakeGhostty();
        using var host = Create(transport);
        Host(host, transport);
        transport.Available = false;
        host.Update(false, true, true, 1);
        Assert.Equal(WindowHostState.Returning, host.State);
        transport.Available = true;
        host.Update(false, true, true, 2);
        transport.Frame();
        host.Update(false, true, true, 2.3);
        Assert.Empty(transport.Panels);
        Assert.Equal(WindowHostState.Screen, host.State);
    }

    [Fact]
    public void ReopenDuringPendingReleaseWaitsForOldPanelToGoAway()
    {
        var transport = new FakeGhostty();
        using var host = Create(transport);
        Host(host, transport);
        host.Update(true, false, true, 1);
        host.Update(true, true, true, 1.1);
        Assert.Single(transport.Posts, p => p.StartsWith("adopt "));
        transport.Frame();
        host.Update(true, true, true, 1.4);
        host.Update(true, true, true, 1.7);
        transport.Frame();
        host.Update(true, true, true, 2);
        Assert.True(host.IsHosted);
        Assert.Single(transport.Panels);
    }

    [Fact]
    public void UnacknowledgedAdoptionIsCancelledWithoutInfinitePostSpam()
    {
        var transport = new FakeGhostty();
        using var host = Create(transport);
        host.Update(true, true, true, 0);
        host.Update(true, true, true, 6);
        Assert.Equal(new[] { "adopt " + Name, "release " + Name }, transport.Posts);
        transport.Frame();
        host.Update(true, true, true, 6.3);
        host.Update(true, true, true, 20);
        Assert.Empty(transport.Panels);
        Assert.Equal(2, transport.Posts.Count);
    }

    [Fact]
    public void PlacementRefusalIsReportedAndNotRetriedEveryFrame()
    {
        var transport = new FakeGhostty { PlacementFailure = "pin: no floor" };
        using var host = Create(transport);
        Host(host, transport);
        transport.Frame();
        host.Update(true, true, true, .6);
        host.Update(true, true, true, .9);
        Assert.True(host.IsHosted);
        Assert.Contains("pin: no floor", host.Status);
        Assert.Single(transport.Calls, IsPlacement);
    }

    [Fact]
    public void ConfirmedPlacementClearsEarlierTransientPollingError()
    {
        var transport = new FakeGhostty { ThrowWindowListOnce = true };
        using var host = Create(transport);
        Host(host, transport);
        Assert.True(host.IsHosted);
        Assert.Contains("Snapshot temporarily unavailable", host.Status);
        transport.Frame();
        host.Update(true, true, true, .6);
        Assert.True(host.IsHosted);
        Assert.DoesNotContain("unavailable", host.Status);
        Assert.DoesNotContain("placing", host.Status);
    }

    [Fact]
    public void DisposeReleasesPendingAdoptionOnceAndCannotAcquireAgain()
    {
        var transport = new FakeGhostty();
        var host = Create(transport);
        host.Update(true, true, true, 0);
        host.Dispose();
        host.Dispose();
        host.Update(true, true, true, 10);
        transport.Frame();
        Assert.Empty(transport.Panels);
        Assert.Equal(2, transport.Posts.Count);
        Assert.Equal(WindowHostState.Disposed, host.State);
    }

    [Fact]
    public void LostAdoptionAcknowledgementStillOwnsExactCleanupOnUnload()
    {
        var transport = new FakeGhostty { ThrowAfterEnqueue = true };
        var host = Create(transport);
        host.Update(true, true, true, 0);
        Assert.Equal(WindowHostState.WaitingForHost, host.State);
        host.Dispose();
        transport.Frame();
        Assert.Equal(new[] { "adopt " + Name, "release " + Name }, transport.Posts);
        Assert.Empty(transport.Panels);
    }

    [Fact]
    public void ExplicitReturnThenRetryRetiresOldPanelBeforeReacquiring()
    {
        var transport = new FakeGhostty();
        using var host = Create(transport);
        Host(host, transport);
        host.ReturnToScreen();
        host.Update(true, true, true, 1);
        transport.Frame();
        host.Update(true, true, true, 1.3);
        host.Update(true, true, true, 2);
        Assert.False(host.IsHosted);
        Assert.Single(transport.Posts, p => p.StartsWith("adopt "));
        host.Retry();
        host.Update(true, true, true, 3);
        transport.Frame();
        host.Update(true, true, true, 3.3);
        Assert.True(host.IsHosted);
        Assert.Equal(101, host.PanelId);
    }

    [Fact]
    public void MalformedOrAmbiguousPanelListNeverSendsAnUntrackableAdoption()
    {
        var posts = new List<string>();
        using var malformed = new GhosttyWindowHost(Name, "Test", _ => "{\"ok\":true,\"result\":{}}", posts.Add);
        malformed.Update(true, true, true, 0);
        Assert.Empty(posts);
        Assert.Equal(WindowHostState.WaitingForHost, malformed.State);
        var transport = new FakeGhostty();
        transport.Panels.Add(new(1, "adopted", Name, "pet", "Journal"));
        transport.Panels.Add(new(2, "adopted", Name, "pet", "Journal"));
        using var ambiguous = Create(transport);
        ambiguous.Update(true, true, true, 0);
        Assert.Empty(transport.Posts);
        Assert.Equal(WindowHostState.WaitingForHost, ambiguous.State);
        Assert.Contains("ambiguous", ambiguous.Status);
    }

    [Theory]
    [InlineData("Journal")]
    [InlineData("###Journal")]
    [InlineData("Journal###")]
    [InlineData("Journal###Own\nrelease")]
    public void UnstableOrMultilineWindowNamesAreRejected(string name)
    {
        Assert.Throws<ArgumentException>(() => new GhosttyWindowHost(name, "Test", _ => "", _ => { }));
    }

    private static GhosttyWindowHost Create(FakeGhostty transport) => new(Name, "XivHud.Journal", transport.Call, transport.Post);
    private static bool IsPlacement(string json) => json.Contains("\"method\":\"panel.place\"");
    private static bool IsFocus(string json) => json.Contains("\"method\":\"window.focus\"");
    private static void HostHud(GhosttyWindowHost host, FakeGhostty transport)
    {
        host.Update(true, true, true, 0, WindowPlacement.Hud);
        transport.Frame();
        host.Update(true, true, true, .3, WindowPlacement.Hud);
        transport.Frame();
        host.Update(true, true, true, .6, WindowPlacement.Hud);
        Assert.True(host.IsHosted);
    }
    private static void Host(GhosttyWindowHost host, FakeGhostty transport)
    {
        host.Update(true, true, true, 0);
        transport.Frame();
        host.Update(true, true, true, .3);
        Assert.True(host.IsHosted);
    }

    private sealed record Panel(int Id, string Kind, string App, string View, string Title);
    private sealed class FakeGhostty
    {
        public bool Available = true;
        public bool ThrowAfterEnqueue;
        public bool ThrowWindowListOnce;
        public string? PlacementFailure;
        public int FocusedId;
        public string FocusedKind = "terminal";
        public string? FocusReply;
        public string? FocusFailure;
        public bool ThrowAfterFocusEnqueue, IgnorePlacementView;
        public List<string> Calls = [];
        public List<string> Posts = [];
        public List<Panel> Panels = [];
        private readonly Queue<string> commands = new();
        private readonly List<object> replies = [];
        private readonly Queue<(long Id, bool Ok, string? Error, int PanelId, string View)> placements = new();
        private readonly Queue<(long Id, int PanelId)> focuses = new();
        private long request;
        private int id = 100;

        public string Call(string json)
        {
            if (!Available) throw new InvalidOperationException("Ghostty IPC is not registered");
            Calls.Add(json);
            using var document = JsonDocument.Parse(json);
            object result;
            switch (document.RootElement.GetProperty("method").GetString())
            {
                case "panel.list":
                    result = new { rev = 1, panels = Panels.Select(p => new { id = p.Id, kind = p.Kind, app = p.App, view = p.View, title = p.Title }) };
                    break;
                case "window.list":
                    if (ThrowWindowListOnce)
                    {
                        ThrowWindowListOnce = false;
                        throw new InvalidOperationException("Snapshot temporarily unavailable");
                    }
                    result = new { requests = replies }; break;
                case "panel.place":
                    var parameters = document.RootElement.GetProperty("params");
                    placements.Enqueue((++request, PlacementFailure == null, PlacementFailure,
                        parameters.GetProperty("id").GetInt32(), parameters.GetProperty("pin").GetString()!));
                    result = new { queued = true, request };
                    break;
                case "window.focus":
                    focuses.Enqueue((++request, document.RootElement.GetProperty("params").GetProperty("id").GetInt32()));
                    if (ThrowAfterFocusEnqueue)
                    {
                        ThrowAfterFocusEnqueue = false;
                        throw new InvalidOperationException("focus acknowledgement lost");
                    }
                    result = new { queued = true, request };
                    break;
                case "focus.get":
                    if (FocusReply != null) return FocusReply;
                    result = FocusedId == 0 ? new { id = 0 } : (object)new { id = FocusedId, kind = FocusedKind };
                    break;
                default: throw new InvalidOperationException("Unexpected IPC method");
            }
            return JsonSerializer.Serialize(new { ok = true, result });
        }

        public void Post(string command)
        {
            if (!Available) throw new InvalidOperationException("Ghostty IPC is not registered");
            Posts.Add(command);
            commands.Enqueue(command);
            if (ThrowAfterEnqueue)
            {
                ThrowAfterEnqueue = false;
                throw new InvalidOperationException("Provider callback failed after enqueueing");
            }
        }

        public void Frame()
        {
            while (commands.TryDequeue(out var command))
            {
                if (command.StartsWith("adopt ")) Panels.Add(new(id++, "adopted", command[6..], "pet", "Journal"));
                else if (command.StartsWith("release ")) Panels.RemoveAll(p => p.Kind == "adopted" && p.App == command[8..]);
                else throw new InvalidOperationException("Unexpected Post command");
            }
            while (placements.TryDequeue(out var placement))
            {
                var index = Panels.FindIndex(p => p.Id == placement.PanelId);
                if (placement.Ok && !IgnorePlacementView && index >= 0)
                    Panels[index] = Panels[index] with { View = placement.View };
                replies.Add(new { request = placement.Id, ok = placement.Ok, error = placement.Error });
            }
            while (focuses.TryDequeue(out var focus))
            {
                var ok = FocusFailure == null && Panels.Any(p => p.Id == focus.PanelId);
                if (ok) FocusedId = focus.PanelId;
                replies.Add(new { request = focus.Id, ok, error = FocusFailure });
            }
        }
    }
}
