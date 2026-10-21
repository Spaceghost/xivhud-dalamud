namespace XivHud.Vitals.Core;

// Value copies only. Never retain a game object, pointer or sheet row in a UI snapshot.
public sealed record Resource(string Label, uint Current, uint Maximum)
{
    public static string SecondaryLabel(uint classJobCategory) => classJobCategory switch { 33 => "CP", 32 => "GP", _ => "MP" };
    public static Resource FromSigned(string label, int current, int maximum)
        => new(label, (uint)Math.Max(0, current), (uint)Math.Max(0, maximum));
    public bool Known => Maximum > 0;
    public float Fraction => Known ? Math.Clamp((float)Current / Maximum, 0, 1) : 0;
    public string Display => Known ? $"{Math.Min(Current, Maximum):N0} / {Maximum:N0}" : "Unavailable";
}

public sealed record CastReading(string Name, float Elapsed, float Total, bool Interruptible)
{
    public static CastReading? Create(bool casting, string name, float elapsed, float total, bool interruptible)
        => !casting || !float.IsFinite(elapsed) || !float.IsFinite(total) || total <= 0 || elapsed < 0 || elapsed >= total
            ? null : new(string.IsNullOrWhiteSpace(name) ? "Casting" : name, elapsed, total, interruptible);

    public float Progress(float ageSeconds) => Math.Clamp((Elapsed + SafeAge(ageSeconds)) / Total, 0, 1);
    public float Remaining(float ageSeconds) => MathF.Max(0, Total - Elapsed - SafeAge(ageSeconds));
    private static float SafeAge(float value) => float.IsFinite(value) ? Math.Clamp(value, 0, .25f) : 0;
}

public sealed record ActorReading(ulong Id, string Name, string Job, uint JobId, int Level,
    Resource Hp, Resource Secondary, int ShieldPercent, bool Dead, CastReading? Cast);

public sealed record PartyReading(int Slot, ActorReading Actor, bool Here, bool Self, bool Leader);

public sealed record VitalsSnapshot(long ObservedAt, ActorReading Player, ActorReading? Target,
    ActorReading? Focus, IReadOnlyList<PartyReading> Party)
{
    public const int FreshnessMilliseconds = 500;
    public bool IsFresh(long now) => now >= ObservedAt && now - ObservedAt <= FreshnessMilliseconds;
    public float AgeSeconds(long now) => now <= ObservedAt ? 0 : Math.Min(.25f, (now - ObservedAt) / 1000f);
}
