using System.Numerics;
using Dalamud.Configuration;
using XivHud.Vitals.Core;

namespace XivHud.Vitals;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool Enabled { get; set; }
    public bool AutoArrange { get; set; }
    public bool Locked { get; set; } = true;
    public bool ShowPlayer { get; set; } = true;
    public bool ShowTarget { get; set; } = true;
    public bool ShowFocus { get; set; } = true;
    public bool ShowParty { get; set; } = true;
    public bool ShowPlayerEffects { get; set; }
    public bool ShowTargetEffects { get; set; }
    public bool TargetEffectsMineOnly { get; set; } = true;
    public Vector2 PlayerEffectsAnchor { get; set; } = PanelLayout.DefaultAnchor(PanelKind.PlayerEffects);
    public Vector2 TargetEffectsAnchor { get; set; } = PanelLayout.DefaultAnchor(PanelKind.TargetEffects);
    public bool Ornament { get; set; } = true;
    public bool DarkPaper { get; set; }
    public float Scale { get; set; } = 1;
    public Vector2 PlayerAnchor { get; set; } = PanelLayout.DefaultAnchor(PanelKind.Player);
    public Vector2 TargetAnchor { get; set; } = PanelLayout.DefaultAnchor(PanelKind.Target);
    public Vector2 FocusAnchor { get; set; } = PanelLayout.DefaultAnchor(PanelKind.Focus);
    public Vector2 PartyAnchor { get; set; } = PanelLayout.DefaultAnchor(PanelKind.Party);

    internal Vector2 Anchor(PanelKind kind) => kind switch
    { PanelKind.Player => PlayerAnchor, PanelKind.Target => TargetAnchor, PanelKind.Focus => FocusAnchor, PanelKind.PlayerEffects => PlayerEffectsAnchor, PanelKind.TargetEffects => TargetEffectsAnchor, _ => PartyAnchor };

    internal void SetAnchor(PanelKind kind, Vector2 value)
    {
        value = PanelLayout.SanitizeAnchor(value, kind);
        switch (kind)
        {
            case PanelKind.Player: PlayerAnchor = value; break;
            case PanelKind.Target: TargetAnchor = value; break;
            case PanelKind.Focus: FocusAnchor = value; break;
            case PanelKind.Party: PartyAnchor = value; break;
            case PanelKind.PlayerEffects: PlayerEffectsAnchor = value; break;
            case PanelKind.TargetEffects: TargetEffectsAnchor = value; break;
        }
    }

    internal void ResetLayout()
    {
        foreach (var kind in Enum.GetValues<PanelKind>()) SetAnchor(kind, PanelLayout.DefaultAnchor(kind));
        Scale = 1; AutoArrange = false;
    }

    internal void Normalize()
    {
        Scale = float.IsFinite(Scale) ? Math.Clamp(Scale, .65f, 2) : 1;
        foreach (var kind in Enum.GetValues<PanelKind>()) SetAnchor(kind, Anchor(kind));
    }
}
