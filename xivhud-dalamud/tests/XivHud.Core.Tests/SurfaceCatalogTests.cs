using System.Text.Json;
using XivHud.Core;

namespace XivHud.Core.Tests;

public sealed class SurfaceCatalogTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-28T20:00:00Z");
    private static string Module(string module, object? state = null, DateTimeOffset? observed = null) => JsonSerializer.Serialize(new { ok = true, result = new { module, observedAt = observed ?? Now, state = state ?? new { ready = true, window = new { open = true, focused = true, actualMode = "world" } } } });
    private static string Panels(params object[] panels) => JsonSerializer.Serialize(new { ok = true, result = new { panels } });
    private static Dictionary<string, string?> All() => new() { ["hud"] = Module("hud"), ["journal"] = Module("journal"), ["character"] = Module("character"), ["vitals"] = Module("vitals") };


    [Fact]
    public void LabelsRetainWholeUnicodeAndRemoveControls()
    {
        Assert.Equal("😀", SurfaceCatalog.Clean("😀x", 1));
        Assert.Equal("a b", SurfaceCatalog.Clean("a\nb"));
    }

    [Fact]
    public void MissingOrBrokenModulesDoNotHideHealthyModules()
    {
        var modules = All(); modules.Remove("character"); modules["vitals"] = "not-json";
        var entries = SurfaceCatalog.Read(modules, null, Now);
        Assert.True(entries.Single(x => x.Id == "module:journal").Available);
        Assert.False(entries.Single(x => x.Id == "module:character").Available);
        Assert.False(entries.Single(x => x.Id == "module:vitals").Available);
    }
    [Fact]
    public void SnapshotBelongingToAnotherModuleIsRefused()
    {
        var entries = SurfaceCatalog.Read(new Dictionary<string, string?> { ["journal"] = Module("character") }, null, Now);
        Assert.False(entries.Single(x => x.Id == "module:journal").Available);
    }
    [Theory]
    [InlineData(-11)] [InlineData(6)]
    public void StaleAndUnexpectedFutureSnapshotsAreNotActionable(int seconds)
    {
        var entries = SurfaceCatalog.Read(new Dictionary<string, string?> { ["journal"] = Module("journal", observed: Now.AddSeconds(seconds)) }, null, Now);
        var row = entries.Single(x => x.Id == "module:journal");
        Assert.False(row.Available); Assert.Empty(SurfaceActions.Create(row, "open"));
    }
    [Fact]
    public void SnapshotRetainsActualPresentationAndFocus()
    {
        var row = SurfaceCatalog.Read(All(), null, Now).Single(x => x.Id == "module:journal");
        Assert.Equal("world", row.Location); Assert.True(row.Open); Assert.True(row.Focused);
    }
    [Fact]
    public void PanelsKeepNestingColoursAndHiddenState()
    {
        var json = Panels(new { id = 3, kind = "terminal", title = "shell", view = "hidden", running = true, agent = "buildbox", workspace = new { key = "child", parent = "root", group = "build", label = "Compiler", depth = 2, colour = 0xbb9af7 } });
        var row = SurfaceCatalog.Read(All(), json, Now).Single(x => x.PanelId == 3);
        Assert.Equal("Compiler", row.Title); Assert.Equal("Workspace / build", row.Group); Assert.Equal("root", row.ParentKey);
        Assert.Equal(2, row.Depth); Assert.Equal(0xbb9af7U, row.Colour); Assert.False(row.Open); Assert.True(row.Available);
    }
    [Fact]
    public void MalformedDuplicateAndUnknownPanelsAreBounded()
    {
        var panels = Enumerable.Range(1, 600).Select(i => (object)new { id = i, kind = "terminal", title = "shell", running = true, view = "tab" }).ToArray();
        Assert.Equal(256, SurfaceCatalog.Read(All(), Panels(panels), Now).Count(x => x.Owner == "ghostty"));
        var mixed = Panels(new { id = 1, kind = "window" }, new { id = 1, kind = "window" }, new { id = -1, kind = "terminal" }, new { id = 2, kind = "unsupported" });
        Assert.Single(SurfaceCatalog.Read(All(), mixed, Now), x => x.Owner == "ghostty");
    }
    [Fact]
    public void SearchIncludesAncestorsAndSurvivesParentCycles()
    {
        UiSurface Entry(string id, string title, string key, string? parent) => new(id, "ghostty", title, "Build", "pet", true, true, false, "Open", Key: key, ParentKey: parent);
        var entries = new[] { Entry("1", "Almanac", "root", "child"), Entry("2", "Compiler", "child", "root") };
        Assert.Equal(2, SurfaceCatalog.Search(entries, "almanac").Count);
        Assert.Single(SurfaceCatalog.Search(entries, "almanac compiler pet"), x => x.Title == "Compiler");
        Assert.Empty(SurfaceCatalog.Search(entries, "does-not-exist"));
    }
    [Fact]
    public void OpeningWorldPanelNeverRequestsCharacterMovement()
    {
        var row = new UiSurface("panel:5", "ghostty", "shell", "Build", "hidden", true, false, false, "Hidden", PanelId: 5);
        var request = Assert.Single(SurfaceActions.Create(row, "open"));
        using var doc = JsonDocument.Parse(request.Json);
        Assert.Equal("panel.focus", doc.RootElement.GetProperty("method").GetString());
        Assert.False(doc.RootElement.GetProperty("params").GetProperty("fly").GetBoolean());
        Assert.Empty(SurfaceActions.Create(row, "settings"));
    }
    [Fact]
    public void ReturnToScreenUsesOnlyExplicitUiMethodsInOrder()
    {
        var row = SurfaceCatalog.Read(All(), null, Now).Single(x => x.Owner == "journal");
        Assert.Equal(new[] { "window.mode", "window.open" }, SurfaceActions.Create(row, "screen").Select(x => x.Method));
        Assert.Equal("window.settings", Assert.Single(SurfaceActions.Create(row with { Id = "module:hud", Owner = "hud" }, "open")).Method);
        Assert.Empty(SurfaceActions.Create(row with { Id = "forged" }, "open"));
    }
    [Fact]
    public void OtherModsUsePublicUiCapabilitiesAndDoNotDuplicateHudModules()
    {
        var rows = SurfaceCatalog.WithPluginUis(SurfaceCatalog.Read(All(), null, Now),
        [new("Almanac", "Almanac old disabled copy", false, true, true), new("Almanac", "Almanac", true, true, true), new("XivRug", "Rug", true, false, true),
         new("XivHud.Journal", "Journal", true, true, true), new("Disabled", "Disabled", false, true, true),
         new("NoUi", "No UI", true, false, false)]);
        Assert.Single(rows, x => x.Id == "module:journal");
        Assert.DoesNotContain(rows, x => x.Id == "plugin:XivHud.Journal" || x.Id == "plugin:NoUi");
        var almanac = rows.Single(x => x.Id == "plugin:Almanac");
        Assert.False(almanac.StateKnown); Assert.Equal("Available", almanac.Status); Assert.True(almanac.Available); Assert.Equal("Almanac", almanac.Title);
        Assert.Equal("ui.open", Assert.Single(SurfaceActions.Create(almanac, "open")).Method);
        var rug = rows.Single(x => x.Id == "plugin:XivRug");
        Assert.Equal("ui.settings", Assert.Single(SurfaceActions.Create(rug, "open")).Method);
        Assert.Empty(SurfaceActions.Create(rows.Single(x => x.Id == "plugin:Disabled"), "open"));
    }

    [Fact]
    public void LoadedLegacyModuleFallsBackToItsPublicUiWhenRichIpcIsMissing()
    {
        var rows = SurfaceCatalog.WithPluginUis(SurfaceCatalog.Read(new Dictionary<string, string?>(), null, Now),
            [new("XivHud.Journal", "Journal", true, true, true)]);
        Assert.DoesNotContain(rows, x => x.Id == "module:journal");
        var journal = Assert.Single(rows, x => x.Id == "plugin:XivHud.Journal");
        Assert.True(journal.Available); Assert.False(journal.StateKnown);
    }

    [Fact]
    public void PublicPluginWindowRequestDoesNotClaimObservedVisibility()
    {
        var sent = new List<string>();
        var session = new NavigatorSession((owner, request) =>
        {
            sent.Add(request);
            return owner.StartsWith("plugin:") ? "{\"ok\":true,\"result\":{\"requested\":true}}" : owner == "ghostty" ? Panels() : Module(owner);
        }, () => [new("XivPiano", "Piano", true, true, true)]);
        session.Refresh(Now);
        Assert.True(session.Activate("plugin:XivPiano", "open", Now));
        Assert.False(session.HasPending); Assert.Equal("UI request sent", session.Message);
        Assert.False(session.Surfaces.Single(x => x.Id == "plugin:XivPiano").StateKnown);
    }

    [Fact]
    public void NestingKeepsChildrenAdjacentToTheirParent()
    {
        UiSurface Row(string key, string title, string? parent) => new(key, "ghostty", title, "Workspace", "pet", true, true, false, "Open", Key: key, ParentKey: parent);
        var result = SurfaceCatalog.Search([Row("a", "A parent", null), Row("b", "B other root", null), Row("c", "Z child", "a")], "");
        Assert.Equal(new[] { "a", "c", "b" }, result.Select(x => x.Id));
    }

    [Theory]
    [InlineData("companion", "pet")] [InlineData("hud", "hud")] [InlineData("world", "here")] [InlineData("ground", "ground")]
    public void PlacementActionsAddressOnlyTheSelectedPanel(string action, string pin)
    {
        var row = new UiSurface("panel:7", "ghostty", "Shell", "Workspace", "pet", true, true, false, "Open", PanelId: 7);
        var request = Assert.Single(SurfaceActions.Create(row, action));
        using var doc = JsonDocument.Parse(request.Json);
        Assert.Equal("panel.place", request.Method);
        Assert.Equal(7, doc.RootElement.GetProperty("params").GetProperty("id").GetInt32());
        Assert.Equal(pin, doc.RootElement.GetProperty("params").GetProperty("pin").GetString());
    }

    [Fact]
    public void PlacingAPanelWaitsForItsOwnRequestResult()
    {
        var completed = false;
        var panel = new { id = 7, kind = "terminal", title = "Shell", running = true, view = "pet", focused = true };
        var session = new NavigatorSession((owner, text) =>
        {
            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.GetProperty("method").GetString() switch
            {
                "status" => Module(owner), "panel.list" => Panels(panel),
                "panel.place" => "{\"ok\":true,\"result\":{\"queued\":true,\"request\":42}}",
                "window.list" => JsonSerializer.Serialize(new { ok = true, result = new { requests = completed ? new[] { new { request = 42, ok = true } } : [] } }),
                _ => null,
            };
        });
        session.Refresh(Now); Assert.True(session.Activate("panel:7", "ground", Now));
        session.Refresh(Now.AddSeconds(1)); Assert.True(session.HasPending);
        completed = true; session.Refresh(Now.AddSeconds(2)); Assert.False(session.HasPending); Assert.Equal("Request applied", session.Message);
    }

    [Fact]
    public void DiscoveryNeverSendsMutatingRequests()
    {
        var requests = new List<string>();
        var session = new NavigatorSession((owner, request) => { requests.Add(request); return owner == "ghostty" ? Panels() : Module(owner); });
        session.Refresh(Now);
        Assert.Equal(5, requests.Count);
        Assert.All(requests, r => Assert.Contains(JsonDocument.Parse(r).RootElement.GetProperty("method").GetString(), new[] { "status", "panel.list" }));
    }
    [Fact]
    public void QueuedActionIsNotReportedAsCompletedAndRefusalsAreVisible()
    {
        var state = "queued";
        var session = new NavigatorSession((owner, request) =>
        {
            using var doc = JsonDocument.Parse(request);
            return doc.RootElement.GetProperty("method").GetString() switch
            {
                "status" => Module(owner), "panel.list" => Panels(),
                "request.status" => JsonSerializer.Serialize(new { ok = true, result = new { state, error = "not_ready" } }),
                _ => "{\"ok\":true,\"result\":{\"accepted\":true,\"requestId\":\"operation-1\"}}",
            };
        });
        session.Refresh(Now);
        Assert.True(session.Activate("module:journal", "open", Now)); Assert.True(session.HasPending);
        session.Refresh(Now.AddSeconds(1)); Assert.True(session.HasPending); Assert.DoesNotContain("applied", session.Message);
        state = "refused"; session.Refresh(Now.AddSeconds(2)); Assert.False(session.HasPending); Assert.Contains("refused", session.Message);
    }
    [Fact]
    public void DisappearingProviderDoesNotThrowOrClaimSuccess()
    {
        var disconnected = false;
        var session = new NavigatorSession((owner, _) => disconnected ? throw new InvalidOperationException() : owner == "ghostty" ? Panels() : Module(owner));
        session.Refresh(Now); disconnected = true;
        Assert.False(session.Activate("module:journal", "open", Now)); Assert.False(session.HasPending);
        session.Refresh(Now); Assert.All(session.Surfaces, x => Assert.False(x.Available));
    }
}
