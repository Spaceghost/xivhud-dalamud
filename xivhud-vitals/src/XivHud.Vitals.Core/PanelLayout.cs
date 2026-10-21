using System.Numerics;

namespace XivHud.Vitals.Core;

public enum PanelKind { Player, Target, Focus, Party, PlayerEffects, TargetEffects }

public readonly record struct PanelRect(Vector2 Position, Vector2 Size, float Scale);

/// <summary>Anchors refer to the available travel, keeping the entire panel on-screen after a resize.</summary>
public static class PanelLayout
{
    public static Vector2 DefaultAnchor(PanelKind kind) => kind switch
    {
        PanelKind.Player => new(.5f, .88f),
        PanelKind.Target => new(.5f, .08f),
        PanelKind.Focus => new(.98f, .28f),
        PanelKind.PlayerEffects => new(.02f, .98f),
        PanelKind.TargetEffects => new(.98f, .02f),
        _ => new(.02f, .38f),
    };

    public static Vector2 BaseSize(PanelKind kind, int partyCount) => kind switch
    {
        PanelKind.Player => new(404, 166),
        PanelKind.Target => new(432, 144),
        PanelKind.Focus => new(336, 144),
        PanelKind.PlayerEffects or PanelKind.TargetEffects => new(432, 270),
        _ => new(310, 50 + Math.Clamp(partyCount, 1, 8) * 58),
    };

    public static Vector2 SanitizeAnchor(Vector2 anchor, PanelKind kind)
        => float.IsFinite(anchor.X) && float.IsFinite(anchor.Y) ? Vector2.Clamp(anchor, Vector2.Zero, Vector2.One) : DefaultAnchor(kind);

    public static PanelRect Fit(PanelKind kind, int partyCount, Vector2 origin, Vector2 viewport, Vector2 anchor, float scale)
    {
        if (!float.IsFinite(viewport.X) || !float.IsFinite(viewport.Y) || viewport.X <= 0 || viewport.Y <= 0 ||
            !float.IsFinite(origin.X) || !float.IsFinite(origin.Y)) return default;
        var size = BaseSize(kind, partyCount);
        // The caller has already combined its preference with the global UI scale.
        scale = float.IsFinite(scale) ? Math.Clamp(scale, .1f, 8) : 1;
        scale = Math.Min(scale, Math.Min(viewport.X / size.X, viewport.Y / size.Y));
        size *= scale;
        return new(origin + Vector2.Max(Vector2.Zero, viewport - size) * SanitizeAnchor(anchor, kind), size, scale);
    }

    public static Vector2 AnchorFromPosition(Vector2 position, Vector2 origin, Vector2 viewport, Vector2 size)
    {
        var travel = Vector2.Max(Vector2.Zero, viewport - size);
        return new(travel.X > 0 ? Math.Clamp((position.X - origin.X) / travel.X, 0, 1) : 0,
            travel.Y > 0 ? Math.Clamp((position.Y - origin.Y) / travel.Y, 0, 1) : 0);
    }
}
