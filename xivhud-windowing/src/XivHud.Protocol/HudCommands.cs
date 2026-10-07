using System.Text.Json;

namespace XivHud.Protocol;

public sealed record HudCommand(string Method, JsonElement Parameters);

/// <summary>Pure, shared validation. No input injection, game actions, or controller leases.</summary>
public static class HudCommands
{
    public static readonly string[] Modules = ["hud", "journal", "character"];
    public static readonly string[] Modes = ["screen", "native_frame", "world"];
    public static readonly string[] Placements = ["Hud", "Companion", "World", "Ground"];
    public static readonly string[] Pages = ["Equipment", "Attributes", "ClassesAndJobs", "GearSets"];

    public static string[] Methods(string module) => module switch
    {
        "hud" => ["hud.set_enabled", "window.settings", "window.close"],
        "journal" => ["window.open", "window.close", "window.settings", "window.mode", "window.original", "journal.select", "journal.bookmark", "journal.show_map"],
        "character" => ["window.open", "window.close", "window.settings", "window.mode", "window.original", "character.page"],
        _ => [],
    };

    public static string? Validate(string module, HudCommand command)
    {
        if (!Methods(module).Contains(command.Method)) return "unsupported_method";
        var p = command.Parameters;
        if (p.ValueKind != JsonValueKind.Object) return "params_must_be_object";
        string[] fields = command.Method switch
        {
            "hud.set_enabled" => ["enabled"],
            "window.mode" => ["mode", "placement"],
            "journal.select" or "journal.show_map" => ["questId"],
            "journal.bookmark" => ["questId", "bookmarked"],
            "character.page" => ["page"],
            _ => [],
        };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in p.EnumerateObject())
            if (!fields.Contains(field.Name) || !seen.Add(field.Name)) return "unknown_or_duplicate_parameter";
        if (command.Method == "hud.set_enabled" && !Boolean(p, "enabled")) return "enabled_must_be_boolean";
        if (command.Method == "window.mode")
        {
            if (!Choice(p, "mode", Modes)) return "invalid_window_mode";
            if (p.TryGetProperty("placement", out _) && (!Choice(p, "placement", Placements) || p.GetProperty("mode").GetString() != "world"))
                return "placement_requires_world_mode";
        }
        if (command.Method.StartsWith("journal.", StringComparison.Ordinal))
        {
            if (!p.TryGetProperty("questId", out var id) || !id.TryGetUInt32(out var value) || value == 0) return "invalid_quest_id";
            if (command.Method == "journal.bookmark" && !Boolean(p, "bookmarked")) return "bookmarked_must_be_boolean";
        }
        if (command.Method == "character.page" && !Choice(p, "page", Pages)) return "invalid_character_page";
        return null;
    }

    private static bool Boolean(JsonElement p, string name) => p.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False;
    private static bool Choice(JsonElement p, string name, string[] choices) => p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && choices.Contains(v.GetString());
}
