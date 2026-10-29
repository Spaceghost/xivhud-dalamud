using System.Collections;
using System.Reflection;
using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Text.ReadOnly;
using XivHud.Combat.Core;
using XivHud.Vitals;
using ActionRow = Lumina.Excel.Sheets.Action;

namespace XivHud.Combat;

internal sealed unsafe class CombatData(IObjectTable objects, ITargetManager targets, IPartyList party,
    IClientState client, IDataManager data, IJobGauges gauges)
{
    private readonly VitalsData vitals = new(objects, targets, party, client, data);
    private readonly Dictionary<uint, string> names = [];
    private readonly Dictionary<(uint, uint), uint> maxima = [];
    private readonly Dictionary<string, (MethodInfo Get, PropertyInfo[] Properties)?> gaugeTypes = [];
    internal static bool BindingsSupported => typeof(RaptureHotbarModule).Assembly.ManifestModule.ModuleVersionId is var id
        && (id == new Guid("43514195-87af-4f1b-86af-fa502aeeada1") || id == new Guid("7fda2a82-4ca8-49d0-9687-4c8eac011509"));

    internal CombatSnapshot? Read(long now, Configuration config)
    {
        if (!BindingsSupported) throw new InvalidOperationException("Combat bindings have not been reviewed for this build");
        if (objects.LocalPlayer is not { } player || vitals.Read(now, true, true) is not { } state) return null;
        var module = RaptureHotbarModule.Instance();
        if (module == null || !module->ModuleReady) return null;
        var manager = ActionManager.Instance();
        var slots = new List<ActionSlot>(48);
        var cross = config.Cross;
        var firstNumber = Math.Clamp(config.FirstBar, 1, cross ? 8 : 10);
        var rows = Math.Clamp(config.Rows, 1, Math.Min(3, (cross ? 8 : 10) - firstNumber + 1));
        var first = (cross ? 10 : 0) + firstNumber - 1;
        for (var bar = first; bar < first + rows; bar++)
        {
            var source = module->Hotbars[bar].Slots;
            for (var index = 0; index < (cross ? 16 : 12); index++)
            {
                ref var slot = ref source[index];
                var token = new SlotIdentity(player.GameObjectId, player.ClassJob.RowId, module->PvPHotbarsActive, bar, index, (byte)slot.CommandType, slot.CommandId, now);
                var shown = slot.ApparentActionId != 0 ? slot.ApparentActionId : slot.CommandId;
                var action = slot.ApparentSlotType == RaptureHotbarModule.HotbarSlotType.Action || slot.CommandType == RaptureHotbarModule.HotbarSlotType.Action;
                string title;
                try { title = ((ReadOnlySeStringSpan)slot.PopUpHelp.AsSpan()).ExtractText(); }
                catch { title = ""; }
                if (action && shown != 0)
                {
                    if (!names.TryGetValue(shown, out var value))
                    {
                        value = data.GetExcelSheet<ActionRow>().GetRowOrDefault(shown)?.Name.ExtractText() ?? "Action";
                        if (names.Count >= 512) names.Clear(); names[shown] = value;
                    }
                    title = value;
                }
                if (string.IsNullOrWhiteSpace(title)) title = slot.IsEmpty ? "Empty" : slot.CommandType.ToString();
                Cooldown? recast = null; string? unavailable = null;
                if (action && shown != 0 && manager != null && !slot.IsEmpty)
                {
                    try
                    {
                        var key = (shown, (uint)player.Level);
                        if (!maxima.TryGetValue(key, out var max))
                        { max = Math.Max(1U, ActionManager.GetMaxCharges(shown, (uint)player.Level)); if (maxima.Count >= 512) maxima.Clear(); maxima[key] = max; }
                        recast = Cooldown.Create(manager->GetRecastTime(ActionType.Action, shown), manager->GetRecastTimeElapsed(ActionType.Action, shown), manager->GetCurrentCharges(shown), max);
                    }
                    catch { unavailable = "Cooldown unavailable"; }
                }
                slots.Add(new(token, title.Length > 160 ? title[..160] : title, slot.IconId, slot.KeybindHintString, slot.IsEmpty, shown, recast, unavailable));
            }
        }
        var (values, status) = ReadGauge(state.Player.Job);
        return new(now, state, slots.AsReadOnly(), values, status) { Cross = cross };
    }

    internal string Execute(SlotIdentity intent, long now, bool enabled, bool preview)
    {
        if (!BindingsSupported || objects.LocalPlayer is not { } player) return "Player or bindings unavailable";
        if (intent.Bar is < 0 or >= 18 || intent.Slot < 0 || intent.Slot >= (intent.Bar < 10 ? 12 : 16)) return "Invalid hotbar slot";
        var module = RaptureHotbarModule.Instance();
        if (module == null || !module->ModuleReady) return "Hotbars unavailable";
        ref var slot = ref module->Hotbars[intent.Bar].Slots[intent.Slot];
        var current = new SlotIdentity(player.GameObjectId, player.ClassJob.RowId, module->PvPHotbarsActive, intent.Bar, intent.Slot, (byte)slot.CommandType, slot.CommandId, now);
        if (!ManualActionGate.CanExecute(intent, current, now, enabled, preview)) return "Slot changed or input expired; click again";
        module->ExecuteSlotById((uint)intent.Bar, (uint)intent.Slot);
        return "Submitted to native hotbar";
    }

    private (IReadOnlyList<GaugeValue>, string) ReadGauge(string job)
    {
        job = job switch { "GLA" => "PLD", "PGL" => "MNK", "MRD" => "WAR", "LNC" => "DRG", "ARC" => "BRD", "CNJ" => "WHM", "THM" => "BLM", "ACN" => "SMN", "ROG" => "NIN", _ => job };
        if (job.Length is < 2 or > 4 || !job.All(char.IsAsciiLetterUpper)) return ([], "No job gauge");
        if (!gaugeTypes.TryGetValue(job, out var schema))
        {
            var type = typeof(JobGaugeBase).Assembly.GetType("Dalamud.Game.ClientState.JobGauge.Types." + job + "Gauge");
            schema = type is not null && typeof(JobGaugeBase).IsAssignableFrom(type)
                ? (typeof(IJobGauges).GetMethod(nameof(IJobGauges.Get))!.MakeGenericMethod(type), type.GetProperties().Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && p.Name != nameof(JobGaugeBase.Address) && p.GetCustomAttribute<ObsoleteAttribute>() is null).Take(20).ToArray()) : null;
            gaugeTypes[job] = schema;
        }
        if (schema is not { } reader) return ([], "No job gauge for this class");
        try
        {
            var gauge = reader.Get.Invoke(gauges, null);
            var result = new List<GaugeValue>();
            foreach (var property in reader.Properties)
            {
                try
                {
                    var value = property.GetValue(gauge);
                    var text = value switch
                    {
                        null => "—", bool b => b ? "Yes" : "No", Enum e => e.ToString(),
                        float f => float.IsFinite(f) ? f.ToString("0.##") : "—", double d => double.IsFinite(d) ? d.ToString("0.##") : "—",
                        IEnumerable list when value is not string => string.Join(", ", list.Cast<object?>().Take(8)),
                        byte or sbyte or short or ushort or int or uint or long or ulong or string => value.ToString(), _ => null,
                    };
                    if (text is not null) result.Add(new(property.Name, text.Length > 100 ? text[..100] : text));
                }
                catch { }
            }
            return (result.AsReadOnly(), result.Count == 0 ? "Gauge unavailable" : "Job resources · timers use native units");
        }
        catch { return ([], "Gauge unavailable"); }
    }
}
