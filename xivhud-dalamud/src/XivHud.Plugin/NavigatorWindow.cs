using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using XivHud.Core;
using FFXIVClientStructs.FFXIV.Client.System.Input;

namespace XivHud.Plugin;

/// <summary>The shared mod shell. Existing module windows and hosts retain ownership of their UI.</summary>
internal sealed class NavigatorWindow
{
    private readonly NavigatorSession session;
    private volatile bool open, searchFocus;
    private string query = "", selected = "";
    private long nextRefresh;
    public bool IsOpen => open;
    private ControllerBroker? controller;
    private Func<bool>? swapButtons;
    private bool controllerFocused;
    private ulong focusGeneration;
    public void ConfigureController(ControllerBroker broker, Func<bool> swap) { controller = broker; swapButtons = swap; }
    private void ReleaseController()
    {
        if (!controllerFocused) return;
        controllerFocused = false; controller?.Release("XivHud.Navigator", focusGeneration);
    }

    public NavigatorWindow(IDalamudPluginInterface pi)
    {
        session = new((owner, request) =>
        {
            if (owner.StartsWith("plugin:", StringComparison.Ordinal))
            {
                var plugin = pi.InstalledPlugins.FirstOrDefault(p => p.InternalName == owner[7..] && p.IsLoaded);
                if (plugin is null) return null;
                using var document = JsonDocument.Parse(request);
                switch (document.RootElement.GetProperty("method").GetString())
                {
                    case "ui.open" when plugin.HasMainUi: plugin.OpenMainUi(); break;
                    case "ui.settings" when plugin.HasConfigUi: plugin.OpenConfigUi(); break;
                    default: return null;
                }
                return "{\"ok\":true,\"result\":{\"requested\":true}}";
            }
            return pi.GetIpcSubscriber<string, string>(owner switch
            {
                "hud" => "XivHud.v1.Call", "journal" => "XivHud.Journal.v1.Call", "character" => "XivHud.Character.v1.Call",
                "vitals" => "XivHud.Vitals.v1.Call", "combat" => "XivHud.Combat.v1.Call", "chat" => "XivHud.Chat.v1.Call", "inventory" => "XivHud.Inventory.v1.Call", "crafting" => "XivHud.Crafting.v1.Call", "windows" => "XivHud.Windows.v1.Call", "ghostty" => "GhosttyDalamud.v1.Call", _ => throw new ArgumentOutOfRangeException(nameof(owner)),
            }).InvokeFunc(request);
        }, () => pi.InstalledPlugins.Select(p => new PluginUiInfo(p.InternalName, p.Name, p.IsLoaded, p.HasMainUi, p.HasConfigUi)).ToArray());
    }
    public void Open() { open = true; searchFocus = true; nextRefresh = 0; }
    public void Close() { open = false; ReleaseController(); }

    public void Draw()
    {
        if ((open || session.HasPending) && Environment.TickCount64 >= nextRefresh)
        { nextRefresh = Environment.TickCount64 + 250; session.Refresh(DateTimeOffset.UtcNow); }
        if (!open) { ReleaseController(); return; }
        ImGui.SetNextWindowSize(new(720, 540), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new(460, 340), new(float.MaxValue, float.MaxValue));
        var shown = open;
        var visible = ImGui.Begin("Mod workspace###XivHud.Navigator", ref shown);
        try
        {
            if (!visible) { ReleaseController(); return; }
            ImGui.TextUnformatted("Your interface, in one place");
            ImGui.TextDisabled("Activities · Character · Combat · Mods · World workspace");
            if (searchFocus) { ImGui.SetKeyboardFocusHere(); searchFocus = false; }
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##surface-search", "Find a mod, host, tab or window…", ref query, 160);
            var entries = SurfaceCatalog.Search(session.Surfaces, query);
            if (!entries.Any(x => x.Id == selected)) selected = entries.FirstOrDefault()?.Id ?? "";
            var followSelection = false;
            var focused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
            var wantsController = focused && !ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId);
            if (wantsController && !controllerFocused)
            { focusGeneration++; if (focusGeneration == 0) focusGeneration++; controllerFocused = true; }
            var ready = wantsController && controller?.Renew("XivHud.Navigator", focusGeneration) == true;
            if (!ready) ReleaseController();
            var pad = ready ? controller?.ReadSnapshot("XivHud.Navigator", focusGeneration) : null;
            var pressed = pad is { Phase: "Active" } frame ? frame.Pressed : 0U;
            var confirm = swapButtons?.Invoke() == true ? GamepadButtonsFlags.Circle : GamepadButtonsFlags.Cross;
            var cancel = swapButtons?.Invoke() == true ? GamepadButtonsFlags.Cross : GamepadButtonsFlags.Circle;
            if (focused && ImGui.IsKeyPressed(ImGuiKey.Escape) || (pressed & (uint)cancel) != 0) shown = false;
            if (focused && entries.Count > 0)
            {
                var index = entries.ToList().FindIndex(x => x.Id == selected);
                if (ImGui.IsKeyPressed(ImGuiKey.DownArrow) || (pressed & (uint)GamepadButtonsFlags.DPadDown) != 0) { selected = entries[(index + 1) % entries.Count].Id; followSelection = true; }
                if (ImGui.IsKeyPressed(ImGuiKey.UpArrow) || (pressed & (uint)GamepadButtonsFlags.DPadUp) != 0) { selected = entries[(index + entries.Count - 1) % entries.Count].Id; followSelection = true; }
                if ((ImGui.IsKeyPressed(ImGuiKey.Enter) || (pressed & (uint)confirm) != 0) && Activate(selected, "open")) shown = false;
            }
            ImGui.Separator();
            if (ImGui.BeginChild("##surfaces", new Vector2(0, -146), false))
            {
                string? group = null;
                foreach (var entry in entries)
                {
                    if (group != entry.Group)
                    { group = entry.Group; ImGui.Spacing(); ImGui.TextColored(new Vector4(.7f, .73f, .8f, 1), Safe(group)); }
                    ImGui.PushID(entry.Id);
                    ImGui.Indent(Math.Min(entry.Depth, 6) * 12);
                    try
                    {
                        var colour = new Vector4(((entry.Colour >> 16) & 255) / 255f, ((entry.Colour >> 8) & 255) / 255f, (entry.Colour & 255) / 255f, entry.Available ? 1 : .45f);
                        ImGui.TextColored(colour, "●"); ImGui.SameLine();
                        if (ImGui.Selectable(Safe(entry.Title) + "###surface", entry.Id == selected, ImGuiSelectableFlags.AllowDoubleClick))
                        {
                            selected = entry.Id;
                            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && Activate(entry.Id, "open")) shown = false;
                        }
                        ImGui.TextDisabled($"    {Safe(entry.Status)} · {Safe(Location(entry.Location))}");
                        if (followSelection && entry.Id == selected) ImGui.SetScrollHereY(.5f);
                    }
                    finally { ImGui.Unindent(Math.Min(entry.Depth, 6) * 12); ImGui.PopID(); }
                }
                if (entries.Count == 0) ImGui.TextDisabled("No matching surfaces");
            }
            ImGui.EndChild();
            ImGui.Separator();
            var choice = entries.FirstOrDefault(x => x.Id == selected);
            ImGui.BeginDisabled(choice is null || !choice.Available);
            if (ImGui.Button(choice?.Owner == "ghostty" ? "Focus / restore" : "Open", new(120, 0)) && Activate(selected, "open")) shown = false;
            if (choice is { Owner: not "ghostty", ConfigUi: true })
            {
                ImGui.SameLine(); if (ImGui.Button("Settings") && Activate(selected, "settings")) shown = false;
                if (choice.Owner is "journal" or "character")
                { ImGui.SameLine(); if (ImGui.Button("Return to screen") && Activate(selected, "screen")) shown = false; }
            }
            ImGui.EndDisabled();
            if (choice is { Owner: "ghostty" or "journal" or "character" })
            {
                ImGui.BeginDisabled(!choice.Available);
                ImGui.SetNextItemWidth(150);
                if (ImGui.BeginCombo("##presentation", "Place…", ImGuiComboFlags.None))
                {
                    foreach (var mode in new[] { ("companion", "Companion"), ("hud", "Dock to HUD"), ("world", "Pin in world"), ("ground", "Place on ground") })
                        if (ImGui.Selectable(mode.Item2) && Activate(selected, mode.Item1)) shown = false;
                    if (choice.Owner != "ghostty" && ImGui.Selectable("Native game frame") && Activate(selected, "native")) shown = false;
                    ImGui.EndCombo();
                }
                ImGui.EndDisabled(); ImGui.SameLine();
            }
            if (ImGui.Button("Refresh")) { nextRefresh = 0; }
            ImGui.TextDisabled("↑ ↓ / D-pad select · Enter / confirm open · Escape / back close");
            if (session.Message.Length > 0) ImGui.TextWrapped(session.Message);
        }
        finally { ImGui.End(); open = shown; if (!open) ReleaseController(); }
    }
    private bool Activate(string id, string action) => session.Activate(id, action, DateTimeOffset.UtcNow);
    private static string Location(string value) => value switch
    {
        "pet" => "Companion", "pin" or "world" => "World", "hud" => "HUD", "tab" => "Terminal tab",
        "min" => "Minimized", "screen" => "Screen", "native_frame" => "Native frame", "full" => "Fullscreen",
        "dropdown" => "Terminal window", "hidden" => "Hidden", "plugin window" => "Mod window", "" => "Unavailable", _ => value,
    };
    private static string Safe(string value) => value.Replace("##", "＃＃", StringComparison.Ordinal);
}
