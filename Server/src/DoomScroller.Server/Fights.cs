using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using DoomScroller.CombatCore;
using Npgsql;
using NpgsqlTypes;

namespace DoomScroller.Server;

public sealed record StoredFight(Guid FightId, string EncounterId, long Seed, string Outcome, bool ReviveFired, string ResultJson, bool Acknowledged);

public enum CommitStatus { Committed, Duplicate, Conflict }

public interface IFightStore
{
    Task<StoredFight?> GetAsync(Guid playerId, Guid fightId, CancellationToken ct);
    /// <summary>Oldest committed fight the client has not acknowledged, or null.</summary>
    Task<StoredFight?> PendingAsync(Guid playerId, CancellationToken ct);
    Task<bool> AcknowledgeAsync(Guid playerId, Guid fightId, CancellationToken ct);
    /// <summary>One transaction: inserts the fight row and writes the profile if its revision still equals expectedRevision.</summary>
    Task<(CommitStatus Status, int Revision)> CommitAsync(Guid playerId, StoredFight fight, string newProfileDocument, int expectedRevision, CancellationToken ct);
}

public sealed class NpgsqlFightStore(NpgsqlDataSource db) : IFightStore
{
    const string Columns = "fight_id, encounter_id, seed, outcome, revive_fired, result::text, acknowledged";

    static StoredFight Read(NpgsqlDataReader r) =>
        new(r.GetGuid(0), r.GetString(1), r.GetInt64(2), r.GetString(3), r.GetBoolean(4), r.GetString(5), r.GetBoolean(6));

    public async Task<StoredFight?> GetAsync(Guid playerId, Guid fightId, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand($"SELECT {Columns} FROM fight_records WHERE player_id = $1 AND fight_id = $2");
        cmd.Parameters.AddWithValue(playerId);
        cmd.Parameters.AddWithValue(fightId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? Read(r) : null;
    }

    public async Task<StoredFight?> PendingAsync(Guid playerId, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand($"SELECT {Columns} FROM fight_records WHERE player_id = $1 AND NOT acknowledged ORDER BY created_utc LIMIT 1");
        cmd.Parameters.AddWithValue(playerId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? Read(r) : null;
    }

    public async Task<bool> AcknowledgeAsync(Guid playerId, Guid fightId, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("UPDATE fight_records SET acknowledged = true WHERE player_id = $1 AND fight_id = $2");
        cmd.Parameters.AddWithValue(playerId);
        cmd.Parameters.AddWithValue(fightId);
        return await cmd.ExecuteNonQueryAsync(ct) == 1;
    }

    public async Task<(CommitStatus, int)> CommitAsync(Guid playerId, StoredFight f, string newDoc, int expectedRevision, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using (var ins = new NpgsqlCommand(
            "INSERT INTO fight_records (player_id, fight_id, encounter_id, seed, outcome, revive_fired, result) " +
            "VALUES ($1, $2, $3, $4, $5, $6, $7) ON CONFLICT DO NOTHING", conn, tx))
        {
            ins.Parameters.AddWithValue(playerId);
            ins.Parameters.AddWithValue(f.FightId);
            ins.Parameters.AddWithValue(f.EncounterId);
            ins.Parameters.AddWithValue(f.Seed);
            ins.Parameters.AddWithValue(f.Outcome);
            ins.Parameters.AddWithValue(f.ReviveFired);
            ins.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = f.ResultJson });
            if (await ins.ExecuteNonQueryAsync(ct) != 1) { await tx.RollbackAsync(ct); return (CommitStatus.Duplicate, 0); }
        }

        await using var upd = new NpgsqlCommand(
            "UPDATE profiles SET document = $2, revision = revision + 1, updated_utc = now() " +
            "WHERE player_id = $1 AND revision = $3 RETURNING revision", conn, tx);
        upd.Parameters.AddWithValue(playerId);
        upd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = newDoc });
        upd.Parameters.AddWithValue(expectedRevision);
        if (await upd.ExecuteScalarAsync(ct) is not int rev) { await tx.RollbackAsync(ct); return (CommitStatus.Conflict, 0); }
        await tx.CommitAsync(ct);
        return (CommitStatus.Committed, rev);
    }
}

/// <summary>Test double. Shares the in-memory profile store so commit is all-or-nothing under one lock.</summary>
public sealed class InMemoryFightStore(InMemoryProfileStore profiles) : IFightStore
{
    readonly Dictionary<(Guid, Guid), StoredFight> rows = new();
    readonly List<(Guid, Guid)> order = new();

    public Task<StoredFight?> GetAsync(Guid playerId, Guid fightId, CancellationToken ct)
    { lock (rows) return Task.FromResult(rows.GetValueOrDefault((playerId, fightId))); }

    public Task<StoredFight?> PendingAsync(Guid playerId, CancellationToken ct)
    {
        lock (rows)
            return Task.FromResult(order.Where(k => k.Item1 == playerId).Select(k => rows[k]).FirstOrDefault(f => !f.Acknowledged));
    }

    public Task<bool> AcknowledgeAsync(Guid playerId, Guid fightId, CancellationToken ct)
    {
        lock (rows)
        {
            if (!rows.TryGetValue((playerId, fightId), out var f)) return Task.FromResult(false);
            rows[(playerId, fightId)] = f with { Acknowledged = true };
            return Task.FromResult(true);
        }
    }

    public async Task<(CommitStatus, int)> CommitAsync(Guid playerId, StoredFight fight, string newDoc, int expectedRevision, CancellationToken ct)
    {
        lock (rows) if (rows.ContainsKey((playerId, fight.FightId))) return (CommitStatus.Duplicate, 0);
        var rev = await profiles.UpdateAsync(playerId, newDoc, expectedRevision, ct);
        if (rev is null) return (CommitStatus.Conflict, 0);
        lock (rows) { rows[(playerId, fight.FightId)] = fight; order.Add((playerId, fight.FightId)); }
        return (CommitStatus.Committed, rev.Value);
    }
}

public interface ISeedSource { int Next(); }
public sealed class RandomSeedSource : ISeedSource { public int Next() => RandomNumberGenerator.GetInt32(int.MaxValue); }

public enum FightStatus { Ok, BadRequest, NotFound, Conflict }

public sealed record FightOutcomeDto(FightStatus Status, StoredFight? Fight = null, int Revision = 0, string? Error = null);

/// <summary>Resolves feed fights on the server. Stateless: everything comes from the store and the cached encounter data.</summary>
public sealed class FightService(IProfileStore profiles, IFightStore fights, EncounterCatalog encounters, ISeedSource seeds, ProfileService profileService)
{
    static readonly JsonSerializerOptions ResultJson = new() { IncludeFields = true };
    const int ReviveStep = 10, ReviveMax = 100, ReviveEffectIndex = 1;

    public async Task<FightOutcomeDto> ResolveAsync(Guid playerId, string? fightIdText, string? encounterId, bool reviveArmed, CancellationToken ct)
    {
        if (!Guid.TryParse(fightIdText, out var fightId) || fightId == Guid.Empty)
            return new(FightStatus.BadRequest, Error: "fightId must be a GUID.");

        // Same id again: hand back what was stored, never a new roll.
        var existing = await fights.GetAsync(playerId, fightId, ct);
        if (existing is not null) return await WithRevision(playerId, existing, ct);

        var enc = encounterId is null ? null : encounters.Find(encounterId);
        if (enc is null) return new(FightStatus.BadRequest, Error: "Unknown encounter id.");
        if (!enc.IsFight) return new(FightStatus.BadRequest, Error: $"Encounter type '{enc.Type}' is not resolved by the server.");

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var row = await profiles.GetAsync(playerId, ct);
            if (row is null) return new(FightStatus.BadRequest, Error: "No profile; upload one first.");
            var doc = (JsonObject)JsonNode.Parse(row.Document)!;
            profileService.ApplyRegen(doc); // server clock; fight starts at the regenerated current max

            var startHp = ProfileService.ReadInt(doc, ProfileService.CurrentMax);
            var revive = reviveArmed ? FindRevive(doc) : null;
            var seed = seeds.Next();
            var input = BuildInput(doc, enc, startHp, seed, revive?.Percent);
            var result = CombatSimulator.Resolve(input);

            if (result.outcome == FightOutcome.Loss) ApplyDefeatPenalty(doc);
            if (result.reviveFired && revive is not null) ConsumeRevive(doc, revive.PoolIndex);

            var fight = new StoredFight(fightId, enc.Id, seed, result.outcome.ToString(), result.reviveFired,
                JsonSerializer.Serialize(result, ResultJson), false);
            var (status, rev) = await fights.CommitAsync(playerId, fight, doc.ToJsonString(), row.Revision, ct);
            switch (status)
            {
                case CommitStatus.Committed: return new(FightStatus.Ok, fight, rev);
                case CommitStatus.Duplicate: return await WithRevision(playerId, (await fights.GetAsync(playerId, fightId, ct))!, ct);
            }
            // Conflict: the profile moved under us. Nothing was written; re-read and redo.
        }
        return new(FightStatus.Conflict, Error: "Profile changed during the fight; retry.");
    }

    public async Task<FightOutcomeDto> PendingAsync(Guid playerId, CancellationToken ct)
    {
        var f = await fights.PendingAsync(playerId, ct);
        return f is null ? new(FightStatus.NotFound) : await WithRevision(playerId, f, ct);
    }

    public async Task<bool> AcknowledgeAsync(Guid playerId, string fightIdText, CancellationToken ct) =>
        Guid.TryParse(fightIdText, out var id) && await fights.AcknowledgeAsync(playerId, id, ct);

    async Task<FightOutcomeDto> WithRevision(Guid playerId, StoredFight f, CancellationToken ct) =>
        new(FightStatus.Ok, f, (await profiles.GetAsync(playerId, ct))?.Revision ?? 0);

    // ---- Input building from the stored profile (SaveData JSON) ----

    public static FightInput BuildInput(JsonObject doc, EncounterDef enc, int startHp, int seed, int? revivePercent)
    {
        var pool = doc["itemPool"] as JsonArray ?? new JsonArray();
        var items = new List<LoadoutItem>();
        foreach (var e in (doc["equipped"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
        {
            var idx = Int(e, "itemIndex");
            if (idx < 0 || idx >= pool.Count || pool[idx] is not JsonObject snap) continue;
            items.Add(ToLoadoutItem(Int(e, "slot"), snap));
        }
        var attrs = (doc["attributeValues"] as JsonArray)?.Select(n => n is JsonValue v && v.TryGetValue<int>(out var i) ? i : 0).ToArray();
        return new FightInput
        {
            player = LoadoutMapper.BuildPlayer(items, attrs ?? new int[0], startHp),
            enemy = new EnemySideInput
            {
                maxHp = enc.Health, attackDamage = enc.AttackDamage, attackSpeed = enc.AttackSpeed,
                canAttack = true, damageMultipliers = enc.Multipliers
            },
            seed = seed,
            reviveArmed = revivePercent is not null,
            revivePercent = revivePercent ?? 0
        };
    }

    static LoadoutItem ToLoadoutItem(int slot, JsonObject s) => new()
    {
        slot = slot,
        category = Int(s, "category"),
        powerValue = Int(s, "powerValue"),
        attackSpeed = Flt(s, "attackSpeed"),
        isRanged = Bool(s, "isRanged"),
        damageType = Int(s, "damageType"),
        scaling = Objs(s, "attributeScalingEntries").Select(o => new ScalingInput { attribute = Int(o, "attribute"), coefficient = Flt(o, "coefficient") }).ToArray(),
        abilities = Objs(s, "abilities").Select(a => new LoadoutAbility
        {
            name = a["abilityName"]?.GetValue<string>() ?? "", powerBonus = Int(a, "powerBonus"), cooldownSeconds = Flt(a, "cooldownSeconds"),
            onCastEffects = Objs(a, "onCastEffects").Select(e => new EffectInput
            {
                effectType = (StatusEffectType)Int(e, "effectType"), magnitude = Flt(e, "magnitude"),
                durationSeconds = Flt(e, "durationSeconds"), tickIntervalSeconds = Flt(e, "tickIntervalSeconds")
            }).ToArray()
        }).ToArray(),
        passives = Objs(s, "passiveEffects").Select(p => new PassiveInput
        {
            effectType = (StatusEffectType)Int(p, "effectType"), magnitude = Flt(p, "magnitude"), tickIntervalSeconds = Flt(p, "tickIntervalSeconds"),
            scalesWithAttribute = Bool(p, "scalesWithAttribute"), scalingAttribute = Int(p, "scalingAttribute"), scalingCoefficient = Flt(p, "scalingCoefficient")
        }).ToArray()
    };

    static IEnumerable<JsonObject> Objs(JsonObject o, string key) => (o[key] as JsonArray)?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>();
    static int Int(JsonObject o, string k) => o[k] is JsonValue v && v.TryGetValue<int>(out var i) ? i : 0;
    static float Flt(JsonObject o, string k) => o[k] is JsonValue v && v.TryGetValue<float>(out var f) ? f : 0f;
    static bool Bool(JsonObject o, string k) => o[k] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    // ---- Revive: best carried potion = highest effective percent (authored base + 10 per upgrade level, cap 100) ----

    sealed record ReviveChoice(int PoolIndex, int Percent);

    static ReviveChoice? FindRevive(JsonObject doc)
    {
        var pool = doc["itemPool"] as JsonArray;
        if (pool is null) return null;
        ReviveChoice? best = null;
        foreach (var n in doc["inventory"] as JsonArray ?? new JsonArray())
        {
            if (n is not JsonValue v || !v.TryGetValue<int>(out var idx) || idx < 0 || idx >= pool.Count) continue;
            if (pool[idx] is not JsonObject item || Int(item, "potionEffect") != ReviveEffectIndex) continue;
            var level = Objs(doc, "reviveUpgrades").Where(u => Int(u, "itemIndex") == idx).Select(u => Int(u, "qualityLevel")).FirstOrDefault();
            var percent = Math.Min(ReviveMax, Int(item, "potionAmount") + level * ReviveStep);
            if (best is null || percent > best.Percent) best = new ReviveChoice(idx, percent);
        }
        return best;
    }

    static void ConsumeRevive(JsonObject doc, int poolIndex)
    {
        var inv = (JsonArray)doc["inventory"]!;
        for (var i = 0; i < inv.Count; i++)
            if (inv[i] is JsonValue v && v.TryGetValue<int>(out var idx) && idx == poolIndex) { inv.RemoveAt(i); return; }
    }

    // PlayerHealth.ApplyDefeatPenalty: 25% of true max (rounded half-to-even like Mathf.RoundToInt), never below the floor.
    static void ApplyDefeatPenalty(JsonObject doc)
    {
        var trueMax = ProfileService.ReadInt(doc, ProfileService.TrueMax);
        var penalty = (int)Math.Round(trueMax * ProfileService.DefeatPenaltyFraction, MidpointRounding.ToEven);
        var cur = ProfileService.ReadInt(doc, ProfileService.CurrentMax);
        doc[ProfileService.CurrentMax] = Math.Max(ProfileService.FloorFor(trueMax), cur - penalty);
    }
}
