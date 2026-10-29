using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using XivHud.Combat.Core;
using XivHud.Vitals.Core;
using static XivHud.Style.IllustratedStyle;

namespace XivHud.Combat;

internal sealed class CombatWindow(Configuration config, Action save, ITextureProvider textures, Action<SlotIdentity> execute)
{
    internal bool Preview, SettingsOpen;
    internal string Status = "Disabled", ActionStatus = "";
    internal void Draw(CombatSnapshot? snapshot, bool allowed)
    {
        var now = Environment.TickCount64;
        var state = Preview ? CombatPreview.Create(now, config.Cross, config.FirstBar, config.Rows) : config.Enabled && allowed && snapshot?.IsFresh(now) == true ? snapshot : null;
        if (state is not null) Deck(state, now, allowed);
        if (SettingsOpen) Settings();
    }
    private void Deck(CombatSnapshot state, long now, bool allowed)
    {
        var viewport = ImGui.GetMainViewport(); var cols = state.Cross ? 8 : 12;
        var slotSize = Math.Max(12, Math.Min(45f * config.Scale, (viewport.WorkSize.X - 32) / cols - 5)); var width = cols * (slotSize + 5) + 32; var height = 250 + (int)Math.Ceiling(state.Slots.Count / (double)cols) * (slotSize + 5);
        var size = new Vector2(Math.Min(width, viewport.WorkSize.X), Math.Min(height, viewport.WorkSize.Y));
        var position = viewport.WorkPos + Vector2.Max(Vector2.Zero, viewport.WorkSize - size) * new Vector2(config.X, config.Y);
        ImGui.SetNextWindowSize(size); ImGui.SetNextWindowPos(position);
        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoMove;
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(.06f, .08f, .10f, .94f));
        var drew = ImGui.Begin("Combat deck###XivHud.Combat.Deck", flags);
        try
        {
            if (!drew) return;
            var d = ImGui.GetWindowDrawList(); Frame(d, position, size, 1, true);
            ImGui.TextColored(new Vector4(.9f, .8f, .55f, 1), Preview ? "COMBAT DECK · SAMPLE" : "COMBAT DECK · " + state.Vitals.Player.Job);
            if (!config.LockLayout)
            {
                ImGui.SameLine(); ImGui.Button("Move", new(60, 0));
                if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {
                    var travel = Vector2.Max(Vector2.One, viewport.WorkSize - size);
                    config.X = Math.Clamp((position.X + ImGui.GetIO().MouseDelta.X - viewport.WorkPos.X) / travel.X, 0, 1);
                    config.Y = Math.Clamp((position.Y + ImGui.GetIO().MouseDelta.Y - viewport.WorkPos.Y) / travel.Y, 0, 1);
                }
            }
            var actor = state.Vitals.Player;
            ImGui.TextUnformatted($"HP {actor.Hp.Display}    {actor.Secondary.Label} {actor.Secondary.Display}");
            Cast(state.Vitals.Player.Cast, state.Age(now), "You"); Cast(state.Vitals.Target?.Cast, state.Age(now), state.Vitals.Target?.Name ?? "Target");
            Effects("You", state.Vitals.PlayerEffects, state.Age(now), false);
            if (state.Vitals.Target is not null) Effects("Target", state.Vitals.TargetEffects, state.Age(now), true);
            ImGui.Separator();
            for (var i = 0; i < state.Slots.Count; i++)
            {
                var slot = state.Slots[i]; var cooldown = slot.Recast;
                ImGui.PushID(i);
                var start = ImGui.GetCursorScreenPos();
                ImGui.BeginDisabled(Preview || !config.ClickActions || slot.Empty || !allowed);
                var clicked = ImGui.InvisibleButton("##action", new(slotSize, slotSize));
                ImGui.EndDisabled();
                d.AddRectFilled(start, start + new Vector2(slotSize), C(26, 34, 39), 4);
                try
                {
                    if (slot.Icon != 0 && textures.GetFromGameIcon(new GameIconLookup(slot.Icon)).TryGetWrap(out var icon, out _))
                        d.AddImage(icon.Handle, start + new Vector2(2), start + new Vector2(slotSize - 2));
                    else if (!slot.Empty) d.AddText(start + new Vector2(6, slotSize / 2 - 6), C(219, 209, 179), (i % (state.Cross ? 16 : 12) + 1).ToString());
                }
                catch { }
                if (cooldown is not null && cooldown.FullRemaining(state.Age(now)) > 0)
                {
                    d.AddRectFilled(start + new Vector2(0, slotSize * (1 - cooldown.Fraction(state.Age(now)))), start + new Vector2(slotSize), C(0, 0, 0, 150), 2);
                    d.AddText(start + new Vector2(7, slotSize * .45f), C(255, 237, 202), cooldown.NextRemaining(state.Age(now)).ToString("0.0"));
                }
                d.AddRect(start, start + new Vector2(slotSize), C(148, 133, 91), 4);
                d.AddText(start + new Vector2(3, 1), C(255, 247, 218), slot.Hint);
                if (cooldown is { Maximum: > 1 }) d.AddText(start + new Vector2(slotSize - 20, slotSize - 15), C(255, 247, 218), $"{cooldown.Charges}/{cooldown.Maximum}");
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    ImGui.BeginTooltip(); ImGui.TextUnformatted(slot.Name);
                    ImGui.TextUnformatted($"{(slot.Identity.Bar < 10 ? "Hotbar" : "Cross hotbar")} {(slot.Identity.Bar % 10) + 1} · slot {slot.Identity.Slot + 1}");
                    if (slot.Unavailable is { } reason) ImGui.TextUnformatted(reason);
                    if (Preview) ImGui.TextUnformatted("Sample only · actions are disabled");
                    ImGui.EndTooltip();
                }
                if (clicked && !Preview && config.ClickActions && allowed && !slot.Empty) execute(slot.Identity);
                ImGui.PopID();
                if ((i + 1) % cols != 0) ImGui.SameLine(0, 5);
            }
            ImGui.Separator();
            ImGui.TextColored(new Vector4(.55f, .8f, .9f, 1), state.GaugeStatus);
            ImGui.TextWrapped(string.Join(" · ", state.Gauge.Take(12).Select(x => x.Label + ": " + x.Value)));
            if (ActionStatus.Length > 0) ImGui.TextDisabled(ActionStatus);
            if (!config.LockLayout && ImGui.IsMouseReleased(ImGuiMouseButton.Left)) save();
        }
        finally { ImGui.End(); ImGui.PopStyleColor(); }
    }
    private void Effects(string title, IReadOnlyList<StatusReading>? readings, float age, bool mineOnly)
    {
        var selection = StatusRibbon.Select(readings ?? [], mineOnly);
        ImGui.TextDisabled(title + " effects"); ImGui.SameLine();
        foreach (var effect in selection.Visible)
        {
            ImGui.PushID(title + effect.Slot);
            var position = ImGui.GetCursorScreenPos(); ImGui.InvisibleButton("##effect", new(25, 25));
            var d = ImGui.GetWindowDrawList(); var iconDrawn = false;
            try
            {
                if (effect.Icon != 0 && textures.GetFromGameIcon(new GameIconLookup(effect.Icon)).TryGetWrap(out var icon, out _))
                { d.AddImage(icon.Handle, position, position + new Vector2(25)); iconDrawn = true; }
            }
            catch { }
            if (!iconDrawn) d.AddRect(position, position + new Vector2(25), C(160, 144, 101));
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip(); ImGui.TextUnformatted(effect.Name + " · " + effect.Duration(age));
                if (effect.Stacks > 0) ImGui.TextUnformatted("Stacks: " + effect.Stacks);
                if (effect.Dispellable) ImGui.TextUnformatted("Dispellable"); ImGui.EndTooltip();
            }
            ImGui.PopID(); ImGui.SameLine(0, 3);
        }
        if (selection.Hidden > 0) ImGui.TextDisabled("+" + selection.Hidden); else ImGui.NewLine();
    }

    private static void Cast(CastReading? cast, float age, string owner)
    {
        if (cast is null || cast.Remaining(age) <= 0) return;
        ImGui.ProgressBar(cast.Progress(age), new(-1, 16), $"{owner}: {cast.Name} · {cast.Remaining(age):0.0}s{(cast.Interruptible ? " · INTERRUPTIBLE" : "")}");
    }
    private void Settings()
    {
        var shown = SettingsOpen; ImGui.SetNextWindowSize(new(430, 0), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Combat HUD###XivHud.Combat.Settings", ref shown, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextWrapped("Your saved hotbars, native key hints, cooldowns and job resources. Native hotbars and gauges remain available.");
            var enabled = config.Enabled; if (ImGui.Checkbox("Show combat deck", ref enabled)) { config.Enabled = enabled; Preview = false; save(); }
            var preview = Preview; if (ImGui.Checkbox("Sample preview", ref preview)) Preview = preview;
            var cross = config.Cross; if (ImGui.Checkbox("Cross hotbars", ref cross)) { config.Cross = cross; config.Normalize(); save(); }
            var first = config.FirstBar; if (ImGui.SliderInt("First bar", ref first, 1, config.Cross ? 8 : 10)) { config.FirstBar = first; config.Normalize(); save(); }
            var rows = config.Rows; if (ImGui.SliderInt("Bars", ref rows, 1, 3)) { config.Rows = rows; config.Normalize(); save(); }
            var scale = config.Scale; if (ImGui.SliderFloat("Scale", ref scale, .65f, 2)) { config.Scale = scale; save(); }
            var click = config.ClickActions; if (ImGui.Checkbox("Click slots to use native hotbar actions", ref click)) { config.ClickActions = click; save(); }
            var locked = config.LockLayout; if (ImGui.Checkbox("Lock position", ref locked)) { config.LockLayout = locked; save(); }
            ImGui.TextWrapped("Keyboard and controller bindings stay with the game. A click is sent once to the same saved slot; changing jobs, bars or characters invalidates an older click.");
            ImGui.TextUnformatted(Status);
        }
        ImGui.End(); SettingsOpen = shown;
    }
}
