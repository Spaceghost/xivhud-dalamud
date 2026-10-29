using System.Text.Json;
using XivHud.Windowing;
namespace XivHud.Windowing.Tests;
public class WindowHostRouterTests
{
    private const string Name = "Test###Owner";
    [Fact] public void DefaultUsesIndependentProviderAndSwitchWaitsForRelease()
    {
        var hosted = false;
        var standalonePosts = new List<string>(); var ghosttyPosts = new List<string>();
        string Call(string _) => JsonSerializer.Serialize(new { ok = true, result = new { panels = hosted ? new[] { new { id = 1, kind = "adopted", app = Name, view = "world" } } : [] } });
        using var router = new WindowHostRouter(new FloatingWindowHost(Name, "Owner", Call, standalonePosts.Add), new GhosttyWindowHost(Name, "Owner", Call, ghosttyPosts.Add));
        router.Update(true, true, true, 0); Assert.Equal("adopt " + Name, Assert.Single(standalonePosts)); Assert.Empty(ghosttyPosts);
        hosted = true; router.Update(true, true, true, .3); Assert.True(router.IsHosted);
        router.UseGhostty = true; router.Update(true, true, true, .6);
        Assert.Equal("release " + Name, standalonePosts.Last()); Assert.Empty(ghosttyPosts);
        hosted = false; router.Update(true, true, true, .9);
        Assert.Equal("adopt " + Name, Assert.Single(ghosttyPosts));
    }
    [Fact] public void MissingStandaloneNeverSilentlyStartsGhostty()
    {
        var posts = new List<string>();
        using var router = new WindowHostRouter(new FloatingWindowHost(Name, "Owner", _ => throw new InvalidOperationException("Not loaded"), posts.Add), new GhosttyWindowHost(Name, "Owner", _ => throw new Exception(), posts.Add));
        router.Update(true, true, true, 0);
        Assert.Empty(posts); Assert.False(router.IsHosted); Assert.Equal(WindowHostState.WaitingForHost, router.State);
    }
}
