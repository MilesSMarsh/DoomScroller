using System.Text.Json;
using DoomScroller.CombatCore;

namespace DoomScroller.Server;

public sealed record EncounterDef(string Id, string Type, int Health, int AttackDamage, float AttackSpeed, DamageMultiplierInput[] Multipliers)
{
    /// <summary>Only plain Enemy cards resolve on the server in this slice (Ambush rules, dungeons and resource nodes come later).</summary>
    public bool IsFight => Type == "Enemy";
}

/// <summary>Static encounter data, read once and cached for the life of the process.</summary>
public sealed class EncounterCatalog
{
    // Ordinals of the game's DamageType enum.
    static readonly Dictionary<string, int> DamageTypes = new(StringComparer.OrdinalIgnoreCase)
        { ["Slash"] = 0, ["Blunt"] = 1, ["Pierce"] = 2, ["Arcane"] = 3, ["Elemental"] = 4 };

    readonly Dictionary<string, EncounterDef> byId;
    public EncounterCatalog(IEnumerable<EncounterDef> defs) => byId = defs.ToDictionary(d => d.Id, StringComparer.Ordinal);
    public EncounterDef? Find(string id) => byId.GetValueOrDefault(id);

    public static EncounterCatalog LoadFile(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var defs = new List<EncounterDef>();
        foreach (var e in doc.RootElement.GetProperty("encounters").EnumerateArray())
        {
            var mods = new List<DamageMultiplierInput>();
            if (e.TryGetProperty("damageTypeModifiers", out var arr))
                foreach (var m in arr.EnumerateArray())
                    mods.Add(new DamageMultiplierInput
                    {
                        damageType = DamageTypes[m.GetProperty("damageType").GetString()!],
                        multiplier = m.GetProperty("multiplier").GetSingle()
                    });
            defs.Add(new EncounterDef(e.GetProperty("id").GetString()!, e.GetProperty("type").GetString()!,
                e.GetProperty("enemyHealth").GetInt32(), e.GetProperty("enemyAttackDamage").GetInt32(),
                e.GetProperty("enemyAttackSpeed").GetSingle(), mods.ToArray()));
        }
        return new EncounterCatalog(defs);
    }
}
