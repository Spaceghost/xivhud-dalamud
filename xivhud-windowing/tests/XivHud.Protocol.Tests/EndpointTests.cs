using System.Text.Json;
using XivHud.Protocol;

public class EndpointTests
{
    [Theory]
    [InlineData("inventory")]
    [InlineData("crafting")]
    public void ItemModulesQueueOnlyScreenUiControls(string module)
    {
        using var endpoint = new HudEndpoint(module);
        foreach (var method in new[] { "window.open", "window.close", "window.settings" })
            Assert.True(Reply(endpoint, Request(method)).GetProperty("ok").GetBoolean());
        foreach (var method in new[] { "item.use", "craft.start", "window.mode", "hud.set_enabled" })
            Assert.False(Reply(endpoint, Request(method)).GetProperty("ok").GetBoolean());
        Assert.False(Reply(endpoint, Request("window.open", new { enabled = true })).GetProperty("ok").GetBoolean());
        var applied = new List<string>(); endpoint.Process(c => { applied.Add(c.Method); return null; });
        Assert.Equal(new[] { "window.open", "window.close", "window.settings" }, applied);
    }

    [Theory]
    [InlineData("{\"enabled\":true,\"locked\":true,\"filter\":\"bookmarks\",\"limit\":5}", true)]
    [InlineData("{\"enabled\":false}", true)]
    [InlineData("{\"enabled\":true,\"filter\":\"all\"}", false)]
    [InlineData("{\"enabled\":true,\"limit\":0}", false)]
    [InlineData("{\"enabled\":true,\"limit\":\"3\"}", false)]
    [InlineData("{\"enabled\":true,\"locked\":\"yes\"}", false)]
    [InlineData("{}", false)]
    public void TrackerControlsAcceptOnlyExplicitUiPreferences(string parameters, bool accepted)
    {
        using var endpoint = new HudEndpoint("journal");
        Assert.Equal(accepted, Reply(endpoint, "{\"method\":\"journal.tracker\",\"params\":" + parameters + "}").GetProperty("ok").GetBoolean());
    }

    [Fact]
    public void CombatEndpointCannotTriggerHotbarActions()
    {
        using var endpoint = new HudEndpoint("combat");
        Assert.False(Reply(endpoint, Request("action.execute", new { bar = 1, slot = 1 })).GetProperty("ok").GetBoolean());
        Assert.True(Reply(endpoint, Request("window.open")).GetProperty("ok").GetBoolean());
    }

    [Fact]
    public void NavigatorOpenIsQueuedWithoutChangingHudVisibility()
    {
        using var endpoint = new HudEndpoint("hud");
        endpoint.Publish(new { enabled = false, navigatorOpen = false });
        var reply = Reply(endpoint, Request("navigator.open"));
        Assert.True(reply.GetProperty("result").GetProperty("accepted").GetBoolean());
        Assert.False(Reply(endpoint, Request("status")).GetProperty("result").GetProperty("state").GetProperty("enabled").GetBoolean());
        var methods = new List<string>(); endpoint.Process(c => { methods.Add(c.Method); return null; });
        Assert.Equal(new[] { "navigator.open" }, methods);
    }

    [Fact]
    public void VitalsSupportsOnlyItsScreenControlsAndQueuesThemForTheOwner()
    {
        using var endpoint = new HudEndpoint("vitals");
        endpoint.Publish(new { enabled = false, preview = false });
        var caps = Reply(endpoint, Request("api.version")).GetProperty("result");
        Assert.Equal("vitals", caps.GetProperty("module").GetString());
        Assert.DoesNotContain("window.mode", caps.GetProperty("methods").EnumerateArray().Select(v => v.GetString()));
        foreach (var method in new[] { "window.mode", "window.original", "hud.set_enabled", "controller.renew" })
            Assert.False(Reply(endpoint, Request(method)).GetProperty("ok").GetBoolean());
        foreach (var method in new[] { "window.open", "window.close", "window.settings" })
            Assert.True(Reply(endpoint, Request(method)).GetProperty("ok").GetBoolean());
        var applied = new List<string>(); endpoint.Process(c => { applied.Add(c.Method); return null; });
        Assert.Equal(new[] { "window.open", "window.close", "window.settings" }, applied);
        Assert.False(Reply(endpoint, Request("status")).GetProperty("result").GetProperty("state").GetProperty("enabled").GetBoolean());
    }

    private static JsonElement Reply(HudEndpoint endpoint, string request) => JsonDocument.Parse(endpoint.Call(request)).RootElement.Clone();
    private static string Request(string method, object? parameters = null) => JsonSerializer.Serialize(new { method, @params = parameters ?? new { } });

    [Fact]
    public void ReadsReturnSnapshotsAndNeverExecuteOrQueueChanges()
    {
        using var endpoint = new HudEndpoint("journal");
        endpoint.Publish(new { open = false, selectedQuestId = 42 });
        Assert.Equal(42, Reply(endpoint, Request("status")).GetProperty("result").GetProperty("state").GetProperty("selectedQuestId").GetInt32());
        Assert.True(Reply(endpoint, Request("api.version")).GetProperty("ok").GetBoolean());
        var executions = 0; endpoint.Process(_ => { executions++; return null; });
        Assert.Equal(0, executions);
    }

    [Fact]
    public void ChangesWaitForTheOwnerAndExposeActualRefusal()
    {
        using var endpoint = new HudEndpoint("journal");
        var ack = Reply(endpoint, Request("journal.select", new { questId = 42 })).GetProperty("result");
        var id = ack.GetProperty("requestId").GetString()!;
        var poll = Request("request.status", new { id });
        Assert.Equal("queued", Reply(endpoint, poll).GetProperty("result").GetProperty("state").GetString());
        endpoint.Process(_ => "quest_not_in_accepted_journal");
        var result = Reply(endpoint, poll).GetProperty("result");
        Assert.Equal("refused", result.GetProperty("state").GetString());
        Assert.Equal("quest_not_in_accepted_journal", result.GetProperty("error").GetString());
    }

    [Theory]
    [InlineData("{\"method\":\"window.mode\",\"params\":{\"mode\":\"screen\",\"placement\":\"Hud\"}}")]
    [InlineData("{\"method\":\"window.mode\",\"params\":{\"mode\":\"world\",\"placement\":\"Anywhere\"}}")]
    [InlineData("{\"method\":\"window.open\",\"params\":{\"execute\":\"/say hi\"}}")]
    [InlineData("{\"method\":\"journal.select\",\"params\":{\"questId\":0}}")]
    [InlineData("{\"method\":\"journal.select\",\"params\":{\"questId\":1,\"questId\":2}}")]
    [InlineData("{\"method\":\"controller.renew\",\"params\":{}}")]
    [InlineData("{\"method\":\"journal.bookmark\",\"params\":{\"questId\":42,\"bookmarked\":\"yes\"}}")]
    [InlineData("[]")]
    [InlineData("not json")]
    public void InvalidCommandsNeverReachTheOwner(string input)
    {
        using var endpoint = new HudEndpoint("journal");
        Assert.False(Reply(endpoint, input).GetProperty("ok").GetBoolean());
        endpoint.Process(_ => throw new Exception("Must not execute."));
    }

    [Fact]
    public void QueueAndResultHistoryAreBoundedAndOldIdsAreNotReplayed()
    {
        using var endpoint = new HudEndpoint("character");
        var first = Reply(endpoint, Request("window.open")).GetProperty("result").GetProperty("requestId").GetString();
        for (var i = 1; i < 32; i++) Assert.True(Reply(endpoint, Request("window.open")).GetProperty("ok").GetBoolean());
        Assert.Equal("queue_full", Reply(endpoint, Request("window.open")).GetProperty("error").GetString());
        var executed = 0;
        for (var i = 0; i < 4; i++) endpoint.Process(_ => { executed++; return null; });
        Assert.Equal(32, executed);
        for (var i = 0; i < 40; i++) { Reply(endpoint, Request("window.close")); endpoint.Process(_ => null); }
        Assert.Equal("request_not_found", Reply(endpoint, Request("request.status", new { id = first })).GetProperty("error").GetString());
    }

    [Fact]
    public void AFailedOperationDoesNotBlockLaterRequestsOrExposeExceptionDetails()
    {
        using var endpoint = new HudEndpoint("character");
        var first = Reply(endpoint, Request("window.open")).GetProperty("result").GetProperty("requestId").GetString();
        var second = Reply(endpoint, Request("window.close")).GetProperty("result").GetProperty("requestId").GetString();
        endpoint.Process(c => c.Method == "window.open" ? throw new Exception("private details") : null);
        var failed = Reply(endpoint, Request("request.status", new { id = first })).GetProperty("result");
        Assert.Equal("operation_failed", failed.GetProperty("error").GetString());
        Assert.Equal("applied", Reply(endpoint, Request("request.status", new { id = second })).GetProperty("result").GetProperty("state").GetString());
    }

    [Fact]
    public void DisposeDropsQueuedChangesAndRejectsAllFurtherCalls()
    {
        var endpoint = new HudEndpoint("hud");
        Reply(endpoint, Request("hud.set_enabled", new { enabled = true }));
        endpoint.Dispose(); endpoint.Process(_ => throw new Exception("Must not execute."));
        Assert.Equal("unloaded", Reply(endpoint, Request("status")).GetProperty("error").GetString());
    }

    [Fact]
    public async Task ConcurrentCallersCannotExceedTheBoundedQueue()
    {
        using var endpoint = new HudEndpoint("hud");
        var replies = await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(() => Reply(endpoint, Request("hud.set_enabled", new { enabled = false })))));
        Assert.Equal(32, replies.Count(r => r.GetProperty("ok").GetBoolean()));
        var applied = 0;
        for (var i = 0; i < 5; i++) endpoint.Process(_ => { applied++; return null; });
        Assert.Equal(32, applied);
    }
    [Fact]
    public void ChatNotificationsAreQueuedButOutboundChatIsUnsupported()
    {
        using var endpoint = new HudEndpoint("chat");
        Assert.False(Reply(endpoint, Request("chat.send", new { text = "hello" })).GetProperty("ok").GetBoolean());
        Assert.False(Reply(endpoint, Request("notification.push", new { title = "Title", text = "Body", kind = "Invalid" })).GetProperty("ok").GetBoolean());
        var id = Reply(endpoint, Request("notification.push", new { title = "Title", text = "Body", kind = "Info" })).GetProperty("result").GetProperty("requestId").GetString();
        var applied = 0;
        endpoint.Process(c => { Assert.Equal("notification.push", c.Method); applied++; return null; });
        Assert.Equal(1, applied);
        Assert.Equal("applied", Reply(endpoint, Request("request.status", new { id })).GetProperty("result").GetProperty("state").GetString());
    }

}
