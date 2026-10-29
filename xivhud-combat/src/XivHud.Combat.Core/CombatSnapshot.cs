using XivHud.Vitals.Core;

namespace XivHud.Combat.Core;

public sealed record Cooldown(float Total, float Elapsed, uint Charges, uint Maximum)
{
    public static Cooldown? Create(float total, float elapsed, uint charges, uint maximum)
        => !float.IsFinite(total) || !float.IsFinite(elapsed) || total < 0 || elapsed < 0 || elapsed > total + .05f || maximum is 0 or > 10 || charges > maximum
            ? null : new(total, Math.Min(total, elapsed), charges, maximum);
    public float FullRemaining(float age) => Math.Max(0, Total - Elapsed - SafeAge(age));
    public float NextRemaining(float age)
    {
        if (Charges >= Maximum || FullRemaining(age) <= 0 || Total <= 0) return 0;
        var period = Total / Maximum;
        var remainder = FullRemaining(age) % period;
        return remainder < .001f ? period : remainder;
    }
    public float Fraction(float age) => Total > 0 ? FullRemaining(age) / Total : 0;
    private static float SafeAge(float age) => float.IsFinite(age) ? Math.Clamp(age, 0, .25f) : 0;
}

public sealed record SlotIdentity(ulong Player, uint Job, bool Pvp, int Bar, int Slot, byte Type, uint Command, long ObservedAt);
public sealed record ActionSlot(SlotIdentity Identity, string Name, uint Icon, string Hint, bool Empty, uint DisplayAction, Cooldown? Recast, string? Unavailable);
public sealed record GaugeValue(string Label, string Value);
public sealed record CombatSnapshot(long ObservedAt, VitalsSnapshot Vitals, IReadOnlyList<ActionSlot> Slots, IReadOnlyList<GaugeValue> Gauge, string GaugeStatus)
{
    public bool Cross { get; init; }
    public bool IsFresh(long now) => now >= ObservedAt && now - ObservedAt <= 500;
    public float Age(long now) => Math.Clamp((now - ObservedAt) / 1000f, 0, .25f);
}

/// <summary>Each explicit click is a one-shot intent tied to the observed character, job and slot.</summary>
public static class ManualActionGate
{
    public static bool CanExecute(SlotIdentity intended, SlotIdentity current, long now, bool enabled, bool preview)
        => enabled && !preview && now >= intended.ObservedAt && now - intended.ObservedAt <= 500
        && intended.Player != 0 && intended.Player == current.Player && intended.Job == current.Job && intended.Pvp == current.Pvp
        && intended.Bar is >= 0 and < 18 && intended.Slot >= 0 && intended.Slot < (intended.Bar < 10 ? 12 : 16)
        && intended.Bar == current.Bar && intended.Slot == current.Slot && intended.Type != 0 && intended.Type == current.Type && intended.Command == current.Command;
}

public static class CombatPreview
{
    public static CombatSnapshot Create(long now, bool cross, int first, int rows)
    {
        first = Math.Clamp(first, 1, cross ? 8 : 10); rows = Math.Clamp(rows, 1, Math.Min(3, (cross ? 8 : 10) - first + 1));
        var vitals = VitalsPreview.Create(now);
        var slots = new List<ActionSlot>();
        for (var b = 0; b < rows; b++) for (var s = 0; s < (cross ? 16 : 12); s++)
        {
            var raw = (cross ? 10 : 0) + first - 1 + b;
            var cooldown = s % 4 == 0 ? Cooldown.Create(60, 35, 0, 2) : Cooldown.Create(0, 0, 1, 1);
            slots.Add(new(new(vitals.Player.Id, vitals.Player.JobId, false, raw, s, 1, (uint)(s + 1), now), "Sample action " + (s + 1), 0, (s + 1).ToString(), false, (uint)s + 1, cooldown, null));
        }
        return new(now, vitals, slots.AsReadOnly(), [new("Oath", "65"), new("Resource", "Sample")], "Sample job gauge") { Cross = cross };
    }
}
