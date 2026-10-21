using System.Numerics;

namespace XivHud.Vitals.Core;

/// <summary>Six passive panels around a clear centre, fitted as one group rather than independently.</summary>
public static class HudArrangement
{
    private const float Gap = 12;
    private const float LeftWidth = 310, MiddleWidth = 432, RightWidth = 432;
    private const float GroupWidth = LeftWidth + MiddleWidth + RightWidth + Gap * 4;
    private const float GroupHeight = 270 + 144 + 270 + Gap * 4;

    public static PanelRect Fit(PanelKind kind, int partyCount, Vector2 origin, Vector2 viewport, float preferredScale)
    {
        if (!float.IsFinite(viewport.X) || !float.IsFinite(viewport.Y) || viewport.X <= 0 || viewport.Y <= 0 ||
            !float.IsFinite(origin.X) || !float.IsFinite(origin.Y)) return default;
        var preferred = float.IsFinite(preferredScale) ? Math.Clamp(preferredScale, .1f, 8) : 1;
        var scale = Math.Min(preferred, Math.Min(viewport.X / GroupWidth, viewport.Y / GroupHeight));
        var size = PanelLayout.BaseSize(kind, partyCount) * scale;
        var left = Gap * scale;
        var right = viewport.X - (RightWidth + Gap) * scale;
        var middleLeft = (LeftWidth + Gap * 2) * scale;
        var middleRight = viewport.X - (RightWidth + Gap * 2) * scale;
        var position = kind switch
        {
            PanelKind.Party => new Vector2(left, (viewport.Y - size.Y) / 2),
            PanelKind.TargetEffects => new Vector2(right, Gap * scale),
            PanelKind.PlayerEffects => new Vector2(right, viewport.Y - size.Y - Gap * scale),
            PanelKind.Focus => new Vector2(right + (RightWidth * scale - size.X) / 2, (viewport.Y - size.Y) / 2),
            PanelKind.Target => new Vector2((middleLeft + middleRight - size.X) / 2, Gap * scale),
            _ => new Vector2((middleLeft + middleRight - size.X) / 2, viewport.Y - size.Y - Gap * scale),
        };
        return new(origin + position, size, scale);
    }
}
