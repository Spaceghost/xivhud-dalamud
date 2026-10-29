using System.Collections.Concurrent;
using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using XivHud.Windowing.Dalamud;
using XivHud.Windows.Core;
using static XivHud.Style.IllustratedStyle;

namespace XivHud.Windows;

/// <summary>Owns presentation resources, never a terminal, process or content lifetime.</summary>
public sealed unsafe class Plugin : IDalamudPlugin
{
    private sealed class Panel(int id, string name, Vector3 anchor, uint zone)
    {
        public readonly int Id = id;
        public readonly string Name = name;
        public readonly uint Zone = zone;
        public Vector3 Anchor = anchor;
        public string Placement = "pet";
        public float Width = 2.4f;
        public IDrawListTextureWrap? Texture;
        public Vector2 Position, Size;
        public PanelQuad Quad;
        public bool Visible, Captured, MouseDisabled;
        public ImGuiWindowFlags OriginalFlags;
        public string Status = "Waiting for source window";
    }
    private readonly IDalamudPluginInterface pi;
    private readonly ICommandManager commands;
    private readonly IClientState client;
    private readonly IObjectTable objects;
    private readonly ICondition condition;
    private readonly IGameGui game;
    private readonly ITextureProvider textures;
    private readonly IPluginLog log;
    private readonly List<Panel> panels = [];
    private readonly ConcurrentQueue<(long Id, string Method, string Name, int PanelId, string Placement)> pending = new();
    private readonly Dictionary<long, object> results = [];
    private readonly ICallGateProvider<string, string> call;
    private readonly ICallGateProvider<string, object> post;
    private ICallGateProvider<object>? toggle, settings;
    private ICallGateProvider<bool>? ready;
    private readonly ImGuiContextHookCallback renderHook, inputHook;
    private ImGuiContextPtr context;
    private uint renderId, inputId;
    private int serial, focused;
    private long requestSerial;
    private bool open, disposed, registered, remapped;
    private Vector2 realMouse;
    private string snapshot = "{}", panelSnapshot = "{\"panels\":[],\"requests\":[]}", message = "";
    private string selected = "";
    private string[] available = [];
    private long nextList;
    private uint zone;
    private bool Allowed => client.IsLoggedIn && !game.GameUiHidden && !condition[ConditionFlag.BetweenAreas] && !condition[ConditionFlag.BetweenAreas51]
        && !condition[ConditionFlag.WatchingCutscene] && !condition[ConditionFlag.WatchingCutscene78]
        && !condition[ConditionFlag.OccupiedInCutSceneEvent] && !condition[ConditionFlag.OccupiedInQuestEvent];

    public Plugin(IDalamudPluginInterface pi, ICommandManager commands, IClientState client, IObjectTable objects,
        ICondition condition, IGameGui game, ITextureProvider textures, IPluginLog log)
    {
        this.pi = pi; this.commands = commands; this.client = client; this.objects = objects;
        this.condition = condition; this.game = game; this.textures = textures; this.log = log;
        renderHook = Render; inputHook = Input;
        call = pi.GetIpcProvider<string, string>("XivHud.Windows.v1.Call");
        post = pi.GetIpcProvider<string, object>("XivHud.Windows.v1.Post");
        try
        {
            registered = commands.AddHandler("/xivwindows", new CommandInfo((_, args) =>
            {
                if (args.Trim() == "screen") Enqueue("release.all");
                else if (args.StartsWith("adopt ", StringComparison.Ordinal)) Enqueue("adopt", args[6..]);
                else if (args.StartsWith("release ", StringComparison.Ordinal)) Enqueue("release", args[8..]);
                else Enqueue("window.open");
            }) { HelpMessage = "Floating windows: open manager, adopt FULL###IDENTITY, release FULL###IDENTITY, screen (recover all). No terminal required." });
            if (!registered) throw new InvalidOperationException("/xivwindows is already registered");
            call.RegisterFunc(Call); post.RegisterAction(Post);
            toggle = pi.GetIpcProvider<object>("XivHud.Windows.Toggle"); toggle.RegisterAction(() => Enqueue("window.toggle"));
            settings = pi.GetIpcProvider<object>("XivHud.Windows.OpenSettings"); settings.RegisterAction(Open);
            ready = pi.GetIpcProvider<bool>("XivHud.Windows.Ready"); ready.RegisterFunc(() => !disposed);
            pi.UiBuilder.Draw += Draw; pi.UiBuilder.OpenMainUi += Open; pi.UiBuilder.OpenConfigUi += Open;
            client.Logout += Logout;
        }
        catch { Dispose(); throw; }
    }
    private void Open() => Enqueue("window.open");
    private void Logout(int a, int b) => Enqueue("release.all");
    private long Enqueue(string method, string name = "", int id = 0, string placement = "pet")
    {
        if (disposed || pending.Count >= 32) throw new InvalidOperationException("Window queue unavailable");
        var request = Interlocked.Increment(ref requestSerial);
        pending.Enqueue((request, method, name, id, placement)); return request;
    }
    private static string Ok(object result) => JsonSerializer.Serialize(new { ok = true, result });
    private static string Error(string error) => JsonSerializer.Serialize(new { ok = false, error });
    private string Call(string json)
    {
        if (disposed) return Error("unloaded");
        if (string.IsNullOrEmpty(json) || json.Length > 4096) return Error("request_too_large");
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            var root = doc.RootElement; var method = root.GetProperty("method").GetString();
            var p = root.TryGetProperty("params", out var parameters) && parameters.ValueKind == JsonValueKind.Object ? parameters : default;
            switch (method)
            {
                case "api.version": return Ok(new { version = 1, module = "windows", methods = new[] { "status", "panel.list", "window.list", "window.open", "window.close", "window.settings", "panel.place", "window.focus", "focus.get" } });
                case "status": return "{\"ok\":true,\"result\":" + Volatile.Read(ref snapshot) + "}";
                case "panel.list": case "window.list": return "{\"ok\":true,\"result\":" + Volatile.Read(ref panelSnapshot) + "}";
                case "focus.get": return Ok(new { id = Volatile.Read(ref focused), kind = "window" });
                case "request.status":
                    if (!long.TryParse(p.GetProperty("id").GetString(), out var token)) return Error("invalid_request_id");
                    using (var state = JsonDocument.Parse(Volatile.Read(ref panelSnapshot)))
                    {
                        foreach (var result in state.RootElement.GetProperty("requests").EnumerateArray())
                            if (result.GetProperty("request").GetInt64() == token)
                                return Ok(new { id = token.ToString(), state = result.GetProperty("ok").GetBoolean() ? "applied" : "refused" });
                    }
                    return token > 0 && token <= Interlocked.Read(ref requestSerial) ? Ok(new { id = token.ToString(), state = "queued" }) : Error("request_not_found");
                case "window.open": case "window.settings": case "window.close":
                    var request = Enqueue(method); return Ok(new { accepted = true, requestId = request.ToString(), queued = true, request });
                case "window.focus":
                    return Ok(new { queued = true, request = Enqueue(method, id: p.GetProperty("id").GetInt32()) });
                case "panel.place":
                    var placement = p.GetProperty("pin").GetString()!;
                    if (!WindowIdentity.Placements.Contains(placement)) return Error("invalid_placement");
                    return Ok(new { queued = true, request = Enqueue(method, id: p.GetProperty("id").GetInt32(), placement: placement) });
                default: return Error("unsupported_method");
            }
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException) { return Error("invalid_request"); }
    }
    private void Post(string command)
    {
        if (command.StartsWith("adopt ", StringComparison.Ordinal))
        {
            var name = command[6..]; if (!WindowIdentity.Valid(name)) throw new ArgumentException("Exact window identity required");
            Enqueue("adopt", name);
        }
        else if (command.StartsWith("release ", StringComparison.Ordinal)) Enqueue("release", command[8..]);
        else throw new ArgumentException("Unknown window command");
    }
    private void Process()
    {
        for (var n = 0; n < 8 && pending.TryDequeue(out var op); n++)
        {
            try
            {
                switch (op.Method)
                {
                    case "window.open": case "window.settings": open = true; break;
                    case "window.toggle": open = !open; break;
                    case "window.close": open = false; break;
                    case "release.all": foreach (var item in panels.ToArray()) Release(item); break;
                    case "release": foreach (var item in panels.Where(i => i.Name == op.Name).ToArray()) Release(item); break;
                    case "adopt":
                        if (!Allowed || objects.LocalPlayer is not { } player) throw new InvalidOperationException("Log in and finish the current game transition first");
                        if (!WindowIdentity.Valid(op.Name) || op.Name.EndsWith("###XivHud.Windows", StringComparison.Ordinal)) throw new InvalidOperationException("Select an exact content window");
                        if (panels.Any(i => WindowIdentity.Key(i.Name) == WindowIdentity.Key(op.Name))) break;
                        if (panels.Count >= 16) throw new InvalidOperationException("Maximum 16 floating windows");
                        if (GhosttyOwns(op.Name)) throw new InvalidOperationException("Return this window from Ghostty first");
                        panels.Add(new(++serial, op.Name, player.Position + new Vector3(0, 1.5f, 0), client.TerritoryType));
                        break;
                    case "panel.place": case "window.focus":
                        var panel = panels.SingleOrDefault(i => i.Id == op.PanelId) ?? throw new InvalidOperationException("Window is no longer hosted");
                        if (op.Method == "window.focus")
                        {
                            var root = ImGuiP.FindWindowByName(panel.Name);
                            if (root.IsNull) throw new InvalidOperationException("Source window is not drawn yet");
                            ImGuiP.FocusWindow(root); focused = panel.Id;
                        }
                        else { panel.Placement = op.Placement; if (objects.LocalPlayer is { } local) panel.Anchor = local.Position + new Vector3(0, 1.5f, 0); }
                        break;
                }
                results[op.Id] = new { request = op.Id, ok = true };
            }
            catch (Exception e) { message = e.Message; results[op.Id] = new { request = op.Id, ok = false, error = e.Message }; }
            while (results.Count > 64) results.Remove(results.Keys.Min());
        }
    }
    private bool GhosttyOwns(string name)
    {
        try
        {
            var value = pi.GetIpcSubscriber<string, string>("GhosttyDalamud.v1.Call").InvokeFunc("{\"method\":\"panel.list\"}");
            using var doc = JsonDocument.Parse(value);
            return doc.RootElement.GetProperty("result").GetProperty("panels").EnumerateArray()
                .Any(p => p.TryGetProperty("kind", out var k) && k.GetString() == "adopted" && p.TryGetProperty("app", out var a) && a.GetString() == name);
        } catch { return false; }
    }
    private void EnsureHooks()
    {
        if (renderId != 0) return;
        context = ImGui.GetCurrentContext();
        if (context.IsNull) return;
        var render = new ImGuiContextHook { Type = ImGuiContextHookType.RenderPre, Callback = (void*)System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(renderHook) };
        var input = new ImGuiContextHook { Type = ImGuiContextHookType.NewFramePre, Callback = (void*)System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(inputHook) };
        renderId = ImGuiP.AddContextHook(context, &render); inputId = ImGuiP.AddContextHook(context, &input);
    }
    private void Draw()
    {
        if (disposed) return;
        try
        {
            EnsureHooks(); Process();
            if (zone != client.TerritoryType) { foreach (var item in panels.ToArray()) if (item.Placement is "here" or "ground") Release(item); zone = client.TerritoryType; }
            foreach (var item in panels) { item.Visible = Allowed && Project(item); if (item.Visible && item.Texture is { } texture && item.Captured) DrawPanel(item, texture); }
            var nav = ImGui.GetCurrentContext().NavWindow;
            if (focused != 0 && !panels.Any(p => { var root = ImGuiP.FindWindowByName(p.Name); return p.Id == focused && !root.IsNull && !nav.IsNull && (nav == root || nav.RootWindow == root || nav.RootWindowPopupTree == root); })) focused = 0;
            if (open) Manager();
            snapshot = JsonSerializer.Serialize(new { version = 1, module = "windows", observedAt = DateTimeOffset.UtcNow,
                state = new { ready = true, loggedIn = client.IsLoggedIn, terminals = 0, count = panels.Count, window = new { open, actualMode = "screen", requestedMode = "screen" }, recoveryCommand = "/xivwindows screen" } });
            panelSnapshot = JsonSerializer.Serialize(new { panels = panels.Select(i => new { id = i.Id, kind = "adopted", app = i.Name, title = i.Name.Split("###")[0], view = i.Placement == "pet" ? "world" : i.Placement, focused = focused == i.Id, status = i.Status }), requests = results.Values.ToArray() });
        }
        catch (Exception e) { message = e.Message; log.Warning(e, "Floating windows frame failed; recovering screen windows"); foreach (var item in panels.ToArray()) Release(item); }
    }
    private bool Project(Panel item)
    {
        if (item.Size.X < 32 || item.Size.Y < 32) return false;
        var viewport = ImGui.GetMainViewport();
        var height = item.Width * item.Size.Y / item.Size.X;
        if (item.Placement == "hud")
        {
            var size = new Vector2(Math.Min(viewport.Size.X * .7f, item.Size.X), Math.Min(viewport.Size.Y * .7f, item.Size.Y));
            var a = viewport.Pos + (viewport.Size - size) / 2;
            item.Quad = new(a, a + new Vector2(size.X, 0), a + size, a + new Vector2(0, size.Y)); return true;
        }
        if (objects.LocalPlayer is not { } player) return false;
        var center = item.Placement == "pet" ? player.Position + new Vector3(1.5f + (item.Id - 1) % 3 * .7f, 1.8f, 0) : item.Anchor;
        var horizontal = Vector3.UnitX;
        if (game.WorldToScreen(center, out var middle) && game.WorldToScreen(center + Vector3.UnitX, out var x) && game.WorldToScreen(center + Vector3.UnitZ, out var z))
        {
            var axis = new Vector3(x.X - middle.X, 0, z.X - middle.X);
            if (axis.LengthSquared() > .001f) horizontal = Vector3.Normalize(axis);
        }
        var right = horizontal * item.Width / 2;
        var up = item.Placement == "ground" ? new Vector3(-right.Z, 0, right.X) * height / item.Width : new Vector3(0, height / 2, 0);
        if (item.Placement == "ground") center.Y = player.Position.Y + .05f;
        if (!game.WorldToScreen(center - right + up, out var a0) || !game.WorldToScreen(center + right + up, out var b)
            || !game.WorldToScreen(center + right - up, out var c) || !game.WorldToScreen(center - right - up, out var d)) return false;
        item.Quad = new(a0, b, c, d); return item.Quad.Valid;
    }
    private void DrawPanel(Panel item, IDrawListTextureWrap texture)
    {
        var q = item.Quad; var draw = ImGui.GetBackgroundDrawList();
        draw.AddImageQuad(texture.Handle, q.A, q.B, q.C, q.D, Vector2.Zero, Vector2.UnitX, Vector2.One, Vector2.UnitY);
        draw.AddQuad(q.A, q.B, q.C, q.D, focused == item.Id ? Aether : Gold, focused == item.Id ? 2 : 1);
    }
    private void Render(ImGuiContext* ctx, ImGuiContextHook* hook)
    {
        if (disposed) return;
        foreach (var item in panels)
        {
            try
            {
                var root = ImGuiP.FindWindowByName(item.Name);
                if (root.IsNull) { item.Captured = false; continue; }
                if (!item.MouseDisabled) { item.OriginalFlags = root.Flags; item.MouseDisabled = true; }
                if (!root.Active || root.Hidden || !Allowed) { item.Captured = false; root.Flags |= ImGuiWindowFlags.NoMouseInputs; continue; }
                item.Position = root.Pos; item.Size = root.Size;
                if (!item.Visible || !item.Quad.Valid) { RestoreFlags(item); continue; }
                var captured = NativeWindowCapture.DrawExpanded(item.Name, frame =>
                {
                    item.Position = frame.Data->DisplayPos; item.Size = frame.Data->DisplaySize;
                    item.Texture ??= textures.CreateDrawListTexture("Floating " + item.Name);
                    if (item.Texture.Size != item.Size) item.Texture.Size = item.Size;
                    item.Texture.ClearColor = Vector4.Zero;
                    item.Texture.Draw(new ImDrawDataPtr(frame.Data)); return true;
                });
                item.Captured = captured;
                item.Status = captured ? "Floating · no terminal" : "Waiting for content";
                root.Flags |= ImGuiWindowFlags.NoMouseInputs;
            }
            catch (Exception e) { item.Captured = false; item.Status = "Screen fallback · " + e.Message; RestoreFlags(item); }
        }
        if (remapped) ImGui.GetIO().MousePos = realMouse;
    }
    private void Input(ImGuiContext* ctx, ImGuiContextHook* hook)
    {
        if (disposed) return;
        try
        {
            var c = new ImGuiContextPtr(ctx); var io = ImGui.GetIO();
            if (!remapped) realMouse = io.MousePos;
            for (var i = 0; i < c.InputEventsQueue.Size; i++)
                if (c.InputEventsQueue[i].Type == ImGuiInputEventType.MousePos)
                    realMouse = new(c.InputEventsQueue[i].Union.MousePos.PosX, c.InputEventsQueue[i].Union.MousePos.PosY);
            var hit = Allowed ? panels.LastOrDefault(i => i.Visible && i.Captured && (i.OriginalFlags & ImGuiWindowFlags.NoMouseInputs) == 0 && i.Quad.TryUv(realMouse, out _)) : null;
            if (hit != null && !ScreenCovers(realMouse))
            {
                hit.Quad.TryUv(realMouse, out var uv);
                var target = hit.Position + uv * hit.Size;
                var changed = false;
                for (var i = 0; i < c.InputEventsQueue.Size; i++)
                    if (c.InputEventsQueue[i].Type == ImGuiInputEventType.MousePos)
                    { c.InputEventsQueue.Data[i].Union.MousePos.PosX = target.X; c.InputEventsQueue.Data[i].Union.MousePos.PosY = target.Y; changed = true; }
                if (!changed) io.AddMousePosEvent(target.X, target.Y);
                var root = ImGuiP.FindWindowByName(hit.Name);
                if (!root.IsNull) root.Flags &= ~ImGuiWindowFlags.NoMouseInputs;
                var clicked = false;
                for (var i = 0; i < c.InputEventsQueue.Size; i++)
                    if (c.InputEventsQueue[i].Type == ImGuiInputEventType.MouseButton && c.InputEventsQueue[i].Union.MouseButton.Down != 0) clicked = true;
                if (clicked && !root.IsNull) { ImGuiP.FocusWindow(root); focused = hit.Id; }
                remapped = true;
            }
            else
            {
                if (remapped) io.AddMousePosEvent(realMouse.X, realMouse.Y);
                remapped = false;
                foreach (var item in panels) { var root = ImGuiP.FindWindowByName(item.Name); if (!root.IsNull && item.Captured) root.Flags |= ImGuiWindowFlags.NoMouseInputs; }
            }
        }
        catch (Exception e) { remapped = false; log.Warning(e, "Floating pointer mapping stopped"); }
    }
    private bool ScreenCovers(Vector2 point)
    {
        for (var i = context.Windows.Size - 1; i >= 0; i--)
        {
            var w = context.Windows[i];
            if (w.IsNull || !w.Active || w.Hidden || (w.Flags & ImGuiWindowFlags.NoMouseInputs) != 0) continue;
            if (panels.Any(p => { var root = ImGuiP.FindWindowByName(p.Name); return !root.IsNull && (w == root || w.RootWindow == root || w.RootWindowPopupTree == root); })) continue;
            if (point.X >= w.Pos.X && point.Y >= w.Pos.Y && point.X < w.Pos.X + w.Size.X && point.Y < w.Pos.Y + w.Size.Y) return true;
        }
        return false;
    }
    private void Manager()
    {
        ImGui.SetNextWindowSize(new(650, 460), ImGuiCond.FirstUseEver);
        var drew = ImGui.Begin("Floating windows###XivHud.Windows", ref open);
        try
        {
            if (!drew) return;
            ImGui.TextColored(V(PaleGold), "FLOATING WINDOWS");
            ImGui.TextWrapped("Select an open plugin window. Float it without Ghostty or a terminal. Use Screen to recover it.");
            if (Environment.TickCount64 >= nextList)
            {
                nextList = Environment.TickCount64 + 1000;
                available = Enumerable.Range(0, context.Windows.Size).Select(i => context.Windows[i]).Where(w => !w.IsNull && w.Active && !w.Hidden && (w.Flags & (ImGuiWindowFlags.ChildWindow | ImGuiWindowFlags.Popup | ImGuiWindowFlags.Tooltip)) == 0)
                    .Select(w => System.Runtime.InteropServices.Marshal.PtrToStringUTF8((nint)w.Name) ?? "").Where(n => WindowIdentity.Valid(n) && !n.EndsWith("###XivHud.Windows", StringComparison.Ordinal)).Distinct().Order().ToArray();
            }
            if (ImGui.BeginCombo("Source", selected.Length > 0 ? selected.Split("###")[0] : "Choose an open window"))
            { foreach (var name in available) if (ImGui.Selectable(name, name == selected)) selected = name; ImGui.EndCombo(); }
            ImGui.BeginDisabled(!WindowIdentity.Valid(selected)); if (ImGui.Button("Float selected")) Enqueue("adopt", selected); ImGui.EndDisabled();
            ImGui.SameLine(); if (ImGui.Button("Return all to screen")) Enqueue("release.all");
            if (message.Length > 0) ImGui.TextWrapped(message);
            ImGui.Separator();
            foreach (var item in panels.ToArray())
            {
                ImGui.PushID(item.Id);
                ImGui.TextUnformatted(item.Name.Split("###")[0]);
                ImGui.TextDisabled(item.Status);
                foreach (var (label, pin) in new[] { ("Beside me", "pet"), ("View", "hud"), ("Here", "here"), ("Ground", "ground") })
                { if (ImGui.SmallButton(label)) Enqueue("panel.place", id: item.Id, placement: pin); ImGui.SameLine(); }
                if (ImGui.SmallButton("Screen")) Enqueue("release", item.Name);
                ImGui.SliderFloat("Width (world units)", ref item.Width, .5f, 6);
                ImGui.PopID(); ImGui.Separator();
            }
            ImGui.TextWrapped("Ghostty still hosts terminals and remote desktop windows with its existing controls. Network handoff is not required for local floating windows.");
        }
        finally { ImGui.End(); }
    }
    private static void RestoreFlags(Panel item)
    {
        var root = ImGuiP.FindWindowByName(item.Name);
        if (!root.IsNull && item.MouseDisabled && (item.OriginalFlags & ImGuiWindowFlags.NoMouseInputs) == 0) root.Flags &= ~ImGuiWindowFlags.NoMouseInputs;
        item.MouseDisabled = false;
    }
    private void Release(Panel item)
    {
        RestoreFlags(item); item.Texture?.Dispose(); item.Texture = null; panels.Remove(item);
        if (focused == item.Id) focused = 0;
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        pi.UiBuilder.Draw -= Draw; pi.UiBuilder.OpenMainUi -= Open; pi.UiBuilder.OpenConfigUi -= Open; client.Logout -= Logout;
        if (!context.IsNull) { if (renderId != 0) ImGuiP.RemoveContextHook(context, renderId); if (inputId != 0) ImGuiP.RemoveContextHook(context, inputId); }
        if (remapped) ImGui.GetIO().MousePos = realMouse;
        foreach (var item in panels.ToArray()) Release(item);
        pending.Clear(); toggle?.UnregisterAction(); settings?.UnregisterAction(); ready?.UnregisterFunc(); call.UnregisterFunc(); post.UnregisterAction(); if (registered) commands.RemoveHandler("/xivwindows");
        GC.KeepAlive(renderHook); GC.KeepAlive(inputHook);
    }
}
