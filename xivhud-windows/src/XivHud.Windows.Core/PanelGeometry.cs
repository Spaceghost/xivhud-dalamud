using System.Numerics;
namespace XivHud.Windows.Core;

public readonly record struct PanelQuad(Vector2 A, Vector2 B, Vector2 C, Vector2 D)
{
    public bool Valid => Finite(A) && Finite(B) && Finite(C) && Finite(D)
        && Vector2.Distance(A, B) > 16 && Vector2.Distance(A, D) > 16
        && Vector2.Distance(A, B) < 8192 && Vector2.Distance(A, D) < 8192;
    // Matches ImGui's two image triangles, including the shared diagonal.
    public bool TryUv(Vector2 point, out Vector2 uv)
    {
        uv = default;
        if (!Valid || !Finite(point)) return false;
        if (Triangle(point, A, B, C, out var weights)) { uv = new(weights.X + weights.Y, weights.Y); return true; }
        if (Triangle(point, A, C, D, out weights)) { uv = new(weights.X, weights.X + weights.Y); return true; }
        return false;
    }
    private static bool Triangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c, out Vector2 result)
    {
        result = default;
        var u = b - a; var v = c - a; var q = p - a;
        var det = u.X * v.Y - u.Y * v.X;
        if (!float.IsFinite(det) || MathF.Abs(det) < .001f) return false;
        result = new((q.X * v.Y - q.Y * v.X) / det, (u.X * q.Y - u.Y * q.X) / det);
        return result.X >= 0 && result.Y >= 0 && result.X + result.Y <= 1.00001f;
    }
    private static bool Finite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
}
public static class WindowIdentity
{
    public static bool Valid(string? name) => name is { Length: > 0 and <= 512 } && name == name.Trim() && !name.Any(char.IsControl)
        && !name.StartsWith("##", StringComparison.Ordinal) && (!name.Contains("###", StringComparison.Ordinal) || name.IndexOf("###", StringComparison.Ordinal) + 3 < name.Length);
    public static string Key(string name) => name.Contains("###", StringComparison.Ordinal) ? name[(name.IndexOf("###", StringComparison.Ordinal) + 3)..] : name;
    // Accept the old input as a compatibility alias; it never circles.
    public static string NormalizePlacement(string placement) => placement == "orbit" ? "pet" : placement;
    public static readonly string[] Placements = ["pet", "orbit", "hud", "here", "ground"];
}

public static class WindowLayout
{
    public static Vector2 Fit(Vector2 size, Vector2 available)
    {
        var scale = Math.Min(1, Math.Min(available.X / Math.Max(1, size.X), available.Y / Math.Max(1, size.Y)));
        return size * Math.Max(0, scale);
    }

    /// <summary>Keep a recoverable source origin in the current viewport, including oversized windows.</summary>
    public static Vector2 Recover(Vector2 position, Vector2 size, Vector2 viewportPosition, Vector2 viewportSize)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(size.X) || !float.IsFinite(size.Y)
            || !float.IsFinite(viewportSize.X) || !float.IsFinite(viewportSize.Y)) return viewportPosition;
        return Vector2.Clamp(position, viewportPosition, viewportPosition + Vector2.Max(Vector2.Zero, viewportSize - size));
    }
}
