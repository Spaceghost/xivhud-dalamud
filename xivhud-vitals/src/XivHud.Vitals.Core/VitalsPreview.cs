namespace XivHud.Vitals.Core;

/// <summary>Explicit, local layout fixture. Never used as a fallback for missing live data.</summary>
public static class VitalsPreview
{
    public static VitalsSnapshot Create(long now, bool fullParty = false)
    {
        ActorReading Actor(ulong id, string name, string job, uint jobId, uint hp, uint max, CastReading? cast = null)
            => new(id, name, job, jobId, 100, new("HP", hp, max), new("MP", 8400, 10000), 0, false, cast);
        var player = Actor(1, "Wandering adventurer", "WHM", 24, 82140, 96000,
            CastReading.Create(true, "Cure II", .8f, 2, false)) with { ShieldPercent = 18 };
        var members = new List<PartyReading>
        {
            new(1, Actor(2, "A steadfast companion", "PLD", 19, 128400, 162000), true, false, true),
            new(2, player, true, true, false),
            new(3, Actor(3, "A far-travelled friend", "DRG", 22, 61200, 104000), true, false, false),
            new(4, Actor(4, "A distant companion", "BLM", 25, 0, 0), false, false, false),
        };
        if (fullParty)
            for (var i = 5; i <= 8; i++) members.Add(new(i, Actor((ulong)i, $"Companion {i}", "BRD", 23, i == 7 ? 0u : 75000u, 100000) with { Dead = i == 7 }, true, false, false));
        return new(now, player,
            Actor(20, "The clockwork sentinel", "", 0, 3425000, 5000000, CastReading.Create(true, "Aetheric pulse", 1.4f, 4.5f, true)),
            Actor(21, "The watchful guardian", "", 0, 910000, 1200000), members.ToArray())
        {
            PlayerEffects = [
                new(0, 1, "A sheltering ward", 0, 18.5f, false, false, true, 0),
                new(1, 2, "A lingering ailment", 0, 8, false, true, false, 0),
                new(2, 3, "Aether in reserve", 0, 0, true, false, true, 3),
                new(3, 4, "An uncertain duration", 0, 0, false, false, false, 0),
            ],
            TargetEffects = Enumerable.Range(0, 11).Select(i => new StatusReading(i, (uint)i + 1,
                "A sample effect " + (i + 1), 0, 30 - i, false, i == 1, i % 3 != 0, 0)).ToArray(),
        };
    }
}
