using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using Dalamud.Plugin;
using XivHud.Vitals.Core;
using static XivHud.Style.IllustratedStyle;

namespace XivHud.Vitals;

internal sealed class VitalsWindow : IDisposable
{
    private readonly Configuration config;
    private readonly Action save;
    private readonly ITextureProvider textures;
    private readonly HashSet<uint> failedIcons = [];
    private readonly IFontHandle heading, body, small;
    private bool layoutDirty, fullParty;
    private IReadOnlyList<StatusReading>? cachedPlayerEffects, cachedTargetEffects;
    private StatusSelection playerSelection = new([], 0), targetSelection = new([], 0);
    private bool cachedMineOnly;
    private StatusSelection SelectEffects(PanelKind kind, IReadOnlyList<StatusReading>? effects, bool mineOnly)
    {
        if (kind == PanelKind.PlayerEffects)
        {
            if (!ReferenceEquals(effects, cachedPlayerEffects))
            { cachedPlayerEffects = effects; playerSelection = StatusRibbon.Select(effects ?? [], false); }
            return playerSelection;
        }
        if (!ReferenceEquals(effects, cachedTargetEffects) || cachedMineOnly != mineOnly)
        { cachedTargetEffects = effects; cachedMineOnly = mineOnly; targetSelection = StatusRibbon.Select(effects ?? [], mineOnly); }
        return targetSelection;
    }
    internal bool SettingsOpen { get; set; }
    internal bool Preview { get; set; }
    internal int VisiblePanels { get; private set; }
    private long lastDraw;
    internal int RecentPanels(long now) => now >= lastDraw && now - lastDraw <= 500 ? VisiblePanels : 0;
    internal string DataStatus { get; set; } = "Disabled";

    internal VitalsWindow(IDalamudPluginInterface pi, Configuration config, Action save, ITextureProvider textures)
    {
        this.config = config; this.save = save; this.textures = textures;
        var acquired = new List<IFontHandle>();
        try
        {
            heading = pi.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Jupiter, 23)); acquired.Add(heading);
            body = pi.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Axis, 17)); acquired.Add(body);
            small = pi.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Axis, 14)); acquired.Add(small);
        }
        catch { foreach (var font in acquired) font.Dispose(); throw; }
    }

    internal void Close() { config.Enabled = false; Preview = false; SettingsOpen = false; save(); }

    internal void Draw(VitalsSnapshot? live, bool allowed)
    {
        VisiblePanels = 0;
        var now = Environment.TickCount64;
        lastDraw = now;
        var state = Preview ? VitalsPreview.Create(now, fullParty) : config.Enabled && allowed && live?.IsFresh(now) == true ? live : null;
        if (state is not null)
        {
            if (config.ShowPlayer) ActorPanel(PanelKind.Player, "YOUR AETHER", state.Player, state.AgeSeconds(now));
            if (config.ShowTarget && (state.Target is not null || !config.Locked)) ActorPanel(PanelKind.Target, "TARGET", state.Target, state.AgeSeconds(now));
            if (config.ShowFocus && (state.Focus is not null || !config.Locked)) ActorPanel(PanelKind.Focus, "FOCUS", state.Focus, state.AgeSeconds(now));
            if (config.ShowParty && (state.Party.Count > 0 || !config.Locked)) PartyPanel(state.Party);
            if (config.ShowPlayerEffects || Preview) EffectsPanel(PanelKind.PlayerEffects, state.PlayerEffects, state.AgeSeconds(now));
            if ((config.ShowTargetEffects || Preview) && (state.Target is not null || !config.Locked))
                EffectsPanel(PanelKind.TargetEffects, state.TargetEffects, state.AgeSeconds(now));
        }
        if (layoutDirty && !ImGui.IsMouseDown(ImGuiMouseButton.Left)) { layoutDirty = false; save(); }
        if (SettingsOpen) Settings();
    }

    private void Panel(PanelKind kind, int count, string title, Action<ImDrawListPtr, Vector2, Vector2, float> draw)
    {
        var viewport = ImGui.GetMainViewport();
        var global = ImGuiHelpers.GlobalScale;
        if (!float.IsFinite(global) || global <= 0) global = 1;
        var rect = PanelLayout.Fit(kind, count, viewport.WorkPos, viewport.WorkSize, config.Anchor(kind), config.Scale * global);
        if (rect.Scale <= 0) return;
        ImGui.SetNextWindowPos(rect.Position);
        ImGui.SetNextWindowSize(rect.Size);
        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
            ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav |
            ImGuiWindowFlags.NoBringToFrontOnFocus;
        // A locked HUD is a true passive overlay: no mouse, keyboard, pad or focus ownership.
        if (config.Locked) flags |= ImGuiWindowFlags.NoInputs;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        try
        {
            var visible = ImGui.Begin($"Vitals {kind}###XivHud.Vitals.{kind}", flags);
            try
            {
                if (!visible) return;
                VisiblePanels++;
                var d = ImGui.GetWindowDrawList();
                var p = rect.Position; var size = rect.Size; var s = rect.Scale;
                Frame(d, p, size, s, config.Ornament);
                // A thin vellum ribbon ties the compact HUD to Journal's reading pages.
                var ribbon = p + new Vector2(12, 11) * s;
                d.AddRectFilled(ribbon, p + new Vector2(size.X - 12 * s, 37 * s), config.DarkPaper ? C(44, 61, 62) : C(223, 209, 174), 2 * s);
                Compass(d, p + new Vector2(27, 24) * s, 8 * s, config.DarkPaper ? PaleGold : C(111, 83, 43));
                var textColor = config.DarkPaper ? Ivory : Ink;
                Text(d, p + new Vector2(43, 15) * s, title, 13 * s, textColor, small, size.X - 146 * s);
                Right(d, p + new Vector2(size.X - 20 * s, 16 * s), Preview ? "SAMPLE" : !config.Locked ? "MOVE" : "", 11 * s, textColor);
                d.PushClipRect(p + new Vector2(12 * s, 39 * s), p + size - new Vector2(12 * s), true);
                try { draw(d, p, size, s); }
                finally { d.PopClipRect(); }
                if (!config.Locked)
                {
                    d.AddRect(p + new Vector2(3 * s), p + size - new Vector2(3 * s), Aether, 8 * s);
                    ImGui.SetCursorScreenPos(p);
                    ImGui.InvisibleButton("##move", size);
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Drag to move. Lock panels in /xivvitals settings to pass clicks through.");
                    if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                    {
                        config.SetAnchor(kind, PanelLayout.AnchorFromPosition(p + ImGui.GetIO().MouseDelta,
                            viewport.WorkPos, viewport.WorkSize, size));
                        layoutDirty = true;
                    }
                }
            }
            finally { ImGui.End(); }
        }
        finally { ImGui.PopStyleVar(2); }
    }

    private void ActorPanel(PanelKind kind, string title, ActorReading? actor, float age)
        => Panel(kind, 0, title, (d, p, size, s) =>
        {
            if (actor is null)
            {
                Text(d, p + new Vector2(20, 56) * s, kind == PanelKind.Target ? "No target selected" : "No focus target", 20 * s, Muted, heading, size.X - 40 * s);
                Text(d, p + new Vector2(20, 94) * s, "Layout placeholder · hidden when locked", 13 * s, Muted, small, size.X - 40 * s);
                return;
            }
            var width = size.X - 40 * s;
            Text(d, p + new Vector2(20, 45) * s, actor.Name, 21 * s, Ivory, heading, width);
            var hpColor = actor.Dead ? C(174, 159, 151) : actor.Hp.Known && actor.Hp.Fraction <= .25f ? C(241, 134, 116) : Aether;
            var level = $"Lv. {actor.Level}" + (actor.Job.Length > 0 ? $"  {actor.Job}" : "");
            var healthState = actor.Dead ? " · KO" : actor.Hp.Known && actor.Hp.Fraction <= .25f ? " · LOW HP" : "";
            Text(d, p + new Vector2(20, 70) * s, level + healthState, 12 * s, hpColor, small, width * .37f);
            Right(d, p + new Vector2(size.X - 20 * s, 70 * s), actor.Hp.Known ? $"{Math.Round(actor.Hp.Fraction * 100):0}%  ·  {actor.Hp.Display}" : "HP unavailable", 12 * s, Ivory, width * .62f);
            Meter(d, p + new Vector2(20, 88) * s, new(width, 9 * s), actor.Hp, hpColor, s);
            if (actor.ShieldPercent > 0 && actor.Hp.Known)
            {
                d.AddRectFilled(p + new Vector2(20, 85) * s, p + new Vector2(20 * s + width * Math.Clamp(actor.ShieldPercent / 100f, 0, 1), 87 * s), PaleGold);
            }
            if (kind == PanelKind.Player)
            {
                var resource = actor.Secondary;
                Text(d, p + new Vector2(20, 101) * s, resource.Label, 12 * s, PaleGold, small, width / 3);
                Right(d, p + new Vector2(size.X - 20 * s, 101 * s), resource.Display, 12 * s, Ivory);
                Meter(d, p + new Vector2(20, 118) * s, new(width, 5 * s), resource, C(141, 164, 214), s);
            }
            var castY = kind == PanelKind.Player ? 130 : 107;
            if (actor.Cast is { } cast && cast.Remaining(age) > 0)
            {
                var tint = cast.Interruptible ? C(244, 189, 118) : PaleGold;
                Text(d, p + new Vector2(20, castY) * s, cast.Name, 13 * s, tint, body, width - (cast.Interruptible ? 135 : 55) * s);
                Right(d, p + new Vector2(size.X - 20 * s, (castY + 1) * s), $"{(cast.Interruptible ? "INTERRUPTIBLE · " : "")}{cast.Remaining(age):0.0}s", 11 * s, tint);
                Fill(d, p + new Vector2(20, castY + 20) * s, new(width, 4 * s), cast.Progress(age), tint, s);
            }
            else if (actor.ShieldPercent > 0)
                Text(d, p + new Vector2(20, castY + 2) * s, $"+{actor.ShieldPercent}% barrier", 12 * s, PaleGold, small, width);
        });

    private void PartyPanel(IReadOnlyList<PartyReading> party)
        => Panel(PanelKind.Party, party.Count, "COMPANIONS", (d, p, size, s) =>
        {
            if (party.Count == 0)
            {
                Text(d, p + new Vector2(20, 58) * s, "Your party gathers here.", 16 * s, Muted, body, size.X - 40 * s);
                return;
            }
            for (var i = 0; i < Math.Min(party.Count, 8); i++)
            {
                var member = party[i]; var actor = member.Actor;
                var row = p + new Vector2(20, 45 + i * 58) * s;
                var width = size.X - 40 * s;
                var low = member.Here && actor.Hp.Known && actor.Hp.Fraction <= .25f;
                var color = actor.Dead ? Muted : low ? C(241, 134, 116) : Aether;
                Diamond(d, row + new Vector2(15, 16) * s, 15 * s, member.Self ? Aether : Gold);
                Center(d, row + new Vector2(15, 9) * s, actor.Job.Length > 0 ? actor.Job : "?", 10 * s, member.Self ? Ivory : PaleGold);
                var name = (member.Leader ? "☆ " : "") + actor.Name + (member.Self ? " · you" : "");
                Text(d, row + new Vector2(39, 0) * s, name, 15 * s, Ivory, body, width - 39 * s);
                var info = !member.Here ? "Other area · HP unavailable" : actor.Dead ? "KO" : !actor.Hp.Known ? "HP unavailable" : $"{Math.Round(actor.Hp.Fraction * 100):0}% · {actor.Hp.Display}";
                Text(d, row + new Vector2(39, 20) * s, info, 12 * s, color, small, width - 39 * s);
                Meter(d, row + new Vector2(39, 39) * s, new(width - 39 * s, 5 * s), actor.Hp, color, s);
            }
        });

    private void EffectsPanel(PanelKind kind, IReadOnlyList<StatusReading>? effects, float age)
        => Panel(kind, 0, kind == PanelKind.PlayerEffects ? "YOUR ENCHANTMENTS" : "TARGET EFFECTS", (d, p, size, s) =>
        {
            var mineOnly = kind == PanelKind.TargetEffects && config.TargetEffectsMineOnly;
            var selected = SelectEffects(kind, effects, mineOnly);
            if (selected.Visible.Count == 0)
                Text(d, p + new Vector2(20, 58) * s,
                    effects is null ? "Status data unavailable" : mineOnly ? "No effects applied by you" : "No active effects",
                    16 * s, Muted, body, size.X - 40 * s);
            for (var i = 0; i < selected.Visible.Count; i++)
            {
                var effect = selected.Visible[i];
                var row = p + new Vector2(20 + i % 2 * 196, 49 + i / 2 * 48) * s;
                var tint = effect.Dispellable ? C(244, 189, 118) : effect.Mine ? Aether : PaleGold;
                d.AddRectFilled(row, row + new Vector2(32 * s), C(10, 21, 26), 3 * s);
                var drewIcon = false;
                if (effect.Icon != 0 && !failedIcons.Contains(effect.Icon))
                {
                    try
                    {
                        if (textures.GetFromGameIcon(new GameIconLookup(effect.Icon)).TryGetWrap(out var icon, out _))
                        { d.AddImage(icon.Handle, row, row + new Vector2(32 * s)); drewIcon = true; }
                    }
                    catch
                    {
                        // A missing icon must not hide the status name or the rest of the HUD.
                        if (failedIcons.Count < 512) failedIcons.Add(effect.Icon);
                    }
                }
                if (!drewIcon) Diamond(d, row + new Vector2(16 * s), 9 * s, tint);
                d.AddRect(row, row + new Vector2(32 * s), tint, 3 * s);
                if (effect.Stacks > 0)
                {
                    var badge = row + new Vector2(18, 19) * s;
                    d.AddRectFilled(badge, row + new Vector2(35, 35) * s, C(10, 21, 26), 2 * s);
                    Text(d, badge, effect.Stacks.ToString(), 12 * s, Ivory, small, 22 * s);
                }
                Text(d, row + new Vector2(40, 0) * s, effect.Name, 13 * s, Ivory, body, 144 * s);
                var detail = effect.Duration(age) + (effect.Dispellable ? " · DISPEL" : effect.Mine ? " · YOURS" : "");
                Text(d, row + new Vector2(40, 19) * s, detail, 11 * s, tint, small, 144 * s);
            }
            var footer = selected.Hidden > 0 ? $"+{selected.Hidden} more · full list in native HUD"
                : mineOnly ? "Applied by you · current target" : "Current effects · dispellable first";
            Text(d, p + new Vector2(20, 247) * s, footer, 11 * s, Muted, small, size.X - 40 * s);
        });

    private static void Meter(ImDrawListPtr d, Vector2 p, Vector2 size, Resource resource, uint color, float s)
    {
        Fill(d, p, size, resource.Fraction, color, s);
        if (!resource.Known)
        {
            d.PushClipRect(p, p + size, true);
            try { for (var x = 0f; x < size.X; x += 9 * s) d.AddLine(p + new Vector2(x, size.Y), p + new Vector2(x + size.Y, 0), C(143, 150, 143, 120)); }
            finally { d.PopClipRect(); }
        }
    }

    private static void Fill(ImDrawListPtr d, Vector2 p, Vector2 size, float fraction, uint color, float s)
    {
        d.AddRectFilled(p, p + size, C(10, 21, 26), 2 * s);
        if (fraction > 0) d.AddRectFilled(p, p + new Vector2(size.X * Math.Clamp(fraction, 0, 1), size.Y), color, 2 * s);
        d.AddRect(p, p + size, C(99, 111, 105), 2 * s, ImDrawFlags.None, Math.Max(.5f, s * .6f));
    }

    private static void Text(ImDrawListPtr d, Vector2 p, string text, float size, uint color, IFontHandle font, float width)
    {
        using var scoped = font.Push();
        // Clip unbounded game names to a single row; each neighbouring field owns its rectangle.
        d.PushClipRect(p, p + new Vector2(Math.Max(0, width), size * 1.4f), true);
        try { d.AddText(ImGui.GetFont(), size, p, color, text); }
        finally { d.PopClipRect(); }
    }

    private void Right(ImDrawListPtr d, Vector2 right, string text, float size, uint color, float maxWidth = float.MaxValue)
    {
        using var scoped = small.Push();
        var width = ImGui.CalcTextSize(text).X * size / ImGui.GetFontSize();
        d.PushClipRect(right - new Vector2(Math.Min(width, maxWidth), 0), right + new Vector2(0, size * 1.4f), true);
        try { d.AddText(ImGui.GetFont(), size, right - new Vector2(width, 0), color, text); }
        finally { d.PopClipRect(); }
    }

    private void Center(ImDrawListPtr d, Vector2 center, string text, float size, uint color)
    {
        using var scoped = small.Push();
        var width = ImGui.CalcTextSize(text).X * size / ImGui.GetFontSize();
        d.AddText(ImGui.GetFont(), size, center - new Vector2(width / 2, 0), color, text);
    }

    private void Settings()
    {
        ImGui.SetNextWindowSize(new(470, 0), ImGuiCond.FirstUseEver);
        var open = SettingsOpen;
        var visible = ImGui.Begin("Vitals · Appearance & layout###XivHud.Vitals.Settings", ref open, ImGuiWindowFlags.AlwaysAutoResize);
        try
        {
            if (!visible) return;
            ImGui.TextWrapped("Your aether, your companions, your focus.");
            ImGui.TextWrapped("These panels match Journal and Character. They never hide the native HUD. Keep your native hotbars and job gauges until replacements are ready.");
            ImGui.Separator();
            var enabled = config.Enabled;
            if (ImGui.Checkbox("Show live Vitals HUD", ref enabled)) { config.Enabled = enabled; Preview = false; save(); }
            var locked = config.Locked;
            if (ImGui.Checkbox("Lock panels (click through)", ref locked)) { config.Locked = locked; save(); }
            ImGui.TextWrapped(config.Locked ? "Mouse, keyboard and controller remain with the game." : "Drag any panel to move it. Target and party placeholders appear while editing.");
            var scale = config.Scale;
            if (ImGui.SliderFloat("Panel size", ref scale, .65f, 2, "%.2f")) { config.Scale = scale; layoutDirty = true; }
            var dark = config.DarkPaper;
            if (ImGui.Checkbox("Night vellum ribbons", ref dark)) { config.DarkPaper = dark; save(); }
            var ornament = config.Ornament;
            if (ImGui.Checkbox("Brass ornament", ref ornament)) { config.Ornament = ornament; save(); }
            ImGui.Separator();
            var player = config.ShowPlayer; var target = config.ShowTarget; var focus = config.ShowFocus; var party = config.ShowParty;
            var changed = ImGui.Checkbox("Your aether", ref player);
            ImGui.SameLine(); changed |= ImGui.Checkbox("Target", ref target);
            changed |= ImGui.Checkbox("Focus target", ref focus);
            ImGui.SameLine(); changed |= ImGui.Checkbox("Companions", ref party);
            if (changed) { config.ShowPlayer = player; config.ShowTarget = target; config.ShowFocus = focus; config.ShowParty = party; save(); }
            ImGui.Separator();
            var playerEffects = config.ShowPlayerEffects; var targetEffects = config.ShowTargetEffects;
            var effectsChanged = ImGui.Checkbox("Your enchantments and ailments", ref playerEffects);
            effectsChanged |= ImGui.Checkbox("Target status effects", ref targetEffects);
            var mineOnly = config.TargetEffectsMineOnly;
            effectsChanged |= ImGui.Checkbox("Only effects you applied to the target", ref mineOnly);
            if (effectsChanged) { config.ShowPlayerEffects = playerEffects; config.ShowTargetEffects = targetEffects; config.TargetEffectsMineOnly = mineOnly; save(); }
            ImGui.TextWrapped("Effect ribbons show up to eight entries, with an overflow count. Dispellable effects come first. Icons come from your game. A dash means the timer is unknown; infinity requires permanent-status metadata. These panels never remove effects or cast a cleanse.");
            ImGui.Separator();
            if (ImGui.Button(Preview ? "Stop sample preview" : "Preview sample layout")) Preview = !Preview;
            if (Preview)
            {
                ImGui.Checkbox("Eight-person preview", ref fullParty);
                ImGui.TextWrapped("SAMPLE DATA · This is a layout preview, not live game state. Preview is not saved across plugin reloads.");
            }
            if (ImGui.Button("Reset panel positions and size")) { config.ResetLayout(); save(); }
            ImGui.TextWrapped($"Live data: {DataStatus}");
            ImGui.TextWrapped("Target panels show character targets only. Party order follows the managed party slots, not your native UI sort. Other-area HP is unavailable; unknown HP is hatched. No click targeting or controller capture.");
            ImGui.TextWrapped("Recovery: /xivvitals close hides these panels. /xivhud off restores the native HUD if you hid it separately.");
        }
        finally { ImGui.End(); SettingsOpen = open; }
    }

    public void Dispose() { heading.Dispose(); body.Dispose(); small.Dispose(); }
}
