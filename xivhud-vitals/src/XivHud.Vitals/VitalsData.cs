using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using XivHud.Vitals.Core;

namespace XivHud.Vitals;

/// <summary>Framework-thread managed API reader. No native hooks or game writes.</summary>
internal sealed class VitalsData(IObjectTable objects, ITargetManager targets, IPartyList party,
    IClientState client, IDataManager data)
{
    private readonly Dictionary<uint, (string Abbreviation, uint Category)> jobs = [];
    private readonly Dictionary<uint, string> actions = [];

    internal VitalsSnapshot? Read(long now)
    {
        if (!client.IsLoggedIn || objects.LocalPlayer is not { } local) return null;
        var members = new List<PartyReading>(8);
        for (var i = 0; i < Math.Min(party.Length, 8); i++)
        {
            if (party[i] is not { } member) continue;
            var job = Job(member.ClassJob.RowId);
            var here = member.Territory.RowId == client.TerritoryType;
            // The party API can retain the last HP after a member changes area.
            // Never present that retained value as a live reading or infer death from it.
            var actor = new ActorReading(member.EntityId, member.Name.TextValue, job.Abbreviation,
                member.ClassJob.RowId, member.Level,
                new("HP", here ? member.CurrentHP : 0, here ? member.MaxHP : 0),
                Resource.FromSigned("MP", here ? member.CurrentMP : 0, here ? member.MaxMP : 0), 0,
                here && member.MaxHP > 0 && member.CurrentHP == 0, null);
            members.Add(new(i + 1, actor, here, member.EntityId == local.EntityId, i == party.PartyLeaderIndex));
        }
        return new(now, Actor(local), targets.Target is ICharacter target ? Actor(target) : null,
            targets.FocusTarget is ICharacter focus ? Actor(focus) : null, members.ToArray());
    }

    private ActorReading Actor(ICharacter actor)
    {
        var job = Job(actor.ClassJob.RowId);
        var secondary = Resource.SecondaryLabel(job.Category) switch
        {
            "CP" => new Resource("CP", actor.CurrentCp, actor.MaxCp),
            "GP" => new Resource("GP", actor.CurrentGp, actor.MaxGp),
            _ => new Resource("MP", actor.CurrentMp, actor.MaxMp),
        };
        CastReading? cast = null;
        if (actor is IBattleChara battle && battle.IsCasting)
        {
            // Only action-type 1 indexes the Action sheet. Other native action kinds
            // have separate ID spaces and must never be guessed from that sheet.
            var name = battle.CastActionType == 1 ? ActionName(battle.CastActionId) : "Casting";
            cast = CastReading.Create(true, name, battle.CurrentCastTime, battle.TotalCastTime, battle.IsCastInterruptible);
        }
        return new(actor.GameObjectId, actor.Name.TextValue, job.Abbreviation, actor.ClassJob.RowId, actor.Level,
            new("HP", actor.CurrentHp, actor.MaxHp), secondary, actor.ShieldPercentage, actor.IsDead, cast);
    }

    private (string Abbreviation, uint Category) Job(uint id)
    {
        if (jobs.TryGetValue(id, out var cached)) return cached;
        var row = data.GetExcelSheet<ClassJob>().GetRowOrDefault(id);
        var value = (row?.Abbreviation.ExtractText() ?? "", row?.ClassJobCategory.RowId ?? 0);
        return jobs[id] = value;
    }

    private string ActionName(uint id)
    {
        if (actions.TryGetValue(id, out var cached)) return cached;
        var name = data.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(id)?.Name.ExtractText();
        // Bound the lazy cache even across a very long session.
        if (actions.Count >= 512) actions.Clear();
        return actions[id] = string.IsNullOrWhiteSpace(name) ? "Casting" : name;
    }
}
