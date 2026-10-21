using System.Globalization;

namespace XivHud.Vitals.Core;

/// <summary>Copied managed status data. Param is never assumed to mean stacks without sheet metadata.</summary>
public sealed record StatusReading(int Slot, uint Id, string Name, uint Icon, float RemainingSeconds,
    bool Permanent, bool Dispellable, bool Mine, int Stacks)
{
    public static int StackCount(int parameter, int maximum) => maximum > 1 && parameter > 0 && parameter <= maximum ? parameter : 0;

    public string Duration(float age)
    {
        if (Permanent) return "∞";
        if (!float.IsFinite(RemainingSeconds) || RemainingSeconds <= 0) return "—";
        var elapsed = float.IsFinite(age) ? Math.Clamp(age, 0, .25f) : 0;
        var seconds = Math.Clamp(Math.Ceiling(RemainingSeconds - elapsed), 0, int.MaxValue);
        if (seconds < 1) return "<1s";
        if (seconds < 60) return seconds.ToString("0", CultureInfo.InvariantCulture) + "s";
        if (seconds < 3600) return $"{(int)seconds / 60}:{(int)seconds % 60:00}";
        return $"{(int)seconds / 3600}h {(int)seconds / 60 % 60:00}m";
    }
}

public sealed record StatusSelection(IReadOnlyList<StatusReading> Visible, int Hidden);

public static class StatusRibbon
{
    public const int Capacity = 8;
    public static StatusSelection Select(IReadOnlyList<StatusReading> statuses, bool mineOnly)
    {
        // Preserve native slot order within priorities: countdown changes must not shuffle the HUD.
        // Same status ID from different sources/slots is not a duplicate to discard.
        var selected = statuses.Where(s => s.Id != 0 && (!mineOnly || s.Mine))
            .OrderByDescending(s => s.Dispellable).ThenBy(s => s.Slot).ToArray();
        return new(selected.Take(Capacity).ToArray(), Math.Max(0, selected.Length - Capacity));
    }
}
