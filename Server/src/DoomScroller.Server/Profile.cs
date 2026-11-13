using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using NpgsqlTypes;

namespace DoomScroller.Server;

public sealed record StoredProfile(string Document, int Revision);

public interface IProfileStore
{
    Task<StoredProfile?> GetAsync(Guid playerId, CancellationToken ct);
    /// <summary>Inserts revision 1. False if the player already has a profile.</summary>
    Task<bool> CreateAsync(Guid playerId, string document, CancellationToken ct);
    /// <summary>Compare-and-set: writes and bumps the revision only if it still equals expectedRevision. Returns the new revision, or null.</summary>
    Task<int?> UpdateAsync(Guid playerId, string document, int expectedRevision, CancellationToken ct);
}

public sealed class NpgsqlProfileStore(NpgsqlDataSource db) : IProfileStore
{
    public async Task<StoredProfile?> GetAsync(Guid playerId, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("SELECT document::text, revision FROM profiles WHERE player_id = $1");
        cmd.Parameters.AddWithValue(playerId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new StoredProfile(r.GetString(0), r.GetInt32(1)) : null;
    }

    public async Task<bool> CreateAsync(Guid playerId, string document, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand(
            "INSERT INTO profiles (player_id, document) VALUES ($1, $2) ON CONFLICT DO NOTHING");
        cmd.Parameters.AddWithValue(playerId);
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = document });
        return await cmd.ExecuteNonQueryAsync(ct) == 1;
    }

    public async Task<int?> UpdateAsync(Guid playerId, string document, int expectedRevision, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand(
            "UPDATE profiles SET document = $2, revision = revision + 1, updated_utc = now() " +
            "WHERE player_id = $1 AND revision = $3 RETURNING revision");
        cmd.Parameters.AddWithValue(playerId);
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = document });
        cmd.Parameters.AddWithValue(expectedRevision);
        return await cmd.ExecuteScalarAsync(ct) as int?;
    }
}

public sealed class InMemoryProfileStore : IProfileStore
{
    readonly Dictionary<Guid, StoredProfile> rows = new();
    public Task<StoredProfile?> GetAsync(Guid playerId, CancellationToken ct)
    {
        lock (rows) return Task.FromResult(rows.GetValueOrDefault(playerId));
    }
    public Task<bool> CreateAsync(Guid playerId, string document, CancellationToken ct)
    {
        lock (rows) return Task.FromResult(rows.TryAdd(playerId, new StoredProfile(document, 1)));
    }
    public Task<int?> UpdateAsync(Guid playerId, string document, int expectedRevision, CancellationToken ct)
    {
        lock (rows)
        {
            if (!rows.TryGetValue(playerId, out var cur) || cur.Revision != expectedRevision) return Task.FromResult<int?>(null);
            rows[playerId] = new StoredProfile(document, expectedRevision + 1);
            return Task.FromResult<int?>(expectedRevision + 1);
        }
    }
}

/// <summary>Regen and size settings. Regen matches ProfileController defaults (10 HP / 30 s).</summary>
public sealed record ProfileOptions(int RegenPerInterval = 10, double RegenIntervalSeconds = 30, int MaxDocumentBytes = 1_000_000)
{
    public static ProfileOptions From(IConfiguration c) => new(
        c.GetValue("REGEN_PER_INTERVAL", 10), c.GetValue("REGEN_INTERVAL_SECONDS", 30.0), c.GetValue("PROFILE_MAX_BYTES", 1_000_000));
}

public enum ProfileStatus { Ok, BadRequest, Conflict, Unavailable }

public sealed record ProfileResult(ProfileStatus Status, string? Document = null, int Revision = 0, string? Error = null);

/// <summary>Profile rules: server-owned regen clock and current max HP. Stateless; state lives in the store.</summary>
public sealed class ProfileService(IProfileStore store, ProfileOptions options, TimeProvider time)
{
    const string TrueMax = "healthTrueMax", CurrentMax = "healthCurrentMax", LastRegen = "healthLastRegenUtcTicks";
    const double FloorFraction = 0.2;

    public DateTime Now => time.GetUtcNow().UtcDateTime;

    /// <summary>Null document = first sign-in (new profile marker). Regen is applied to the returned copy, not persisted.</summary>
    public async Task<ProfileResult> GetAsync(Guid playerId, CancellationToken ct)
    {
        var row = await store.GetAsync(playerId, ct);
        if (row is null) return new ProfileResult(ProfileStatus.Ok, null, 0);
        var doc = (JsonObject)JsonNode.Parse(row.Document)!;
        ApplyRegen(doc);
        return new ProfileResult(ProfileStatus.Ok, doc.ToJsonString(), row.Revision);
    }

    public async Task<ProfileResult> PutAsync(Guid playerId, string body, int? revision, CancellationToken ct)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(body) > options.MaxDocumentBytes)
            return new ProfileResult(ProfileStatus.BadRequest, Error: "Document too large.");
        JsonObject incoming;
        try { incoming = JsonNode.Parse(body) as JsonObject ?? throw new JsonException(); }
        catch (JsonException) { return new ProfileResult(ProfileStatus.BadRequest, Error: "Body must be a JSON object."); }

        var row = await store.GetAsync(playerId, ct);
        var trueMax = Math.Max(0, ReadInt(incoming, TrueMax));
        if (row is null)
        {
            // First sign-in: the client's current max is taken, clamped to true max.
            if (revision is not null and not 0) return new ProfileResult(ProfileStatus.Conflict, Revision: 0, Error: "No profile at that revision.");
            incoming[CurrentMax] = Math.Min(ReadInt(incoming, CurrentMax), trueMax);
            incoming[LastRegen] = Now.Ticks;
            var json = incoming.ToJsonString();
            if (await store.CreateAsync(playerId, json, ct)) return new ProfileResult(ProfileStatus.Ok, json, 1);
            return new ProfileResult(ProfileStatus.Conflict, Revision: (await store.GetAsync(playerId, ct))?.Revision ?? 0, Error: "Stale revision.");
        }

        if (revision != row.Revision) return new ProfileResult(ProfileStatus.Conflict, Revision: row.Revision, Error: "Stale revision.");

        // Keep the server's max HP and regen clock; the upload's values are ignored.
        var stored = (JsonObject)JsonNode.Parse(row.Document)!;
        stored[TrueMax] = trueMax;
        ApplyRegen(stored);
        incoming[CurrentMax] = stored[CurrentMax]!.DeepClone();
        incoming[LastRegen] = stored[LastRegen]!.DeepClone();
        var merged = incoming.ToJsonString();
        var newRev = await store.UpdateAsync(playerId, merged, row.Revision, ct);
        return newRev is null
            ? new ProfileResult(ProfileStatus.Conflict, Revision: (await store.GetAsync(playerId, ct))?.Revision ?? 0, Error: "Stale revision.")
            : new ProfileResult(ProfileStatus.Ok, merged, newRev.Value);
    }

    /// <summary>Credits whole regen intervals elapsed on the server clock; capped at true max, floored at 20% of it.</summary>
    void ApplyRegen(JsonObject doc)
    {
        var trueMax = ReadInt(doc, TrueMax);
        var floor = Math.Max(1, (int)Math.Round(trueMax * FloorFraction, MidpointRounding.AwayFromZero));
        var cur = Math.Clamp(ReadInt(doc, CurrentMax), Math.Min(floor, trueMax), trueMax);
        var now = Now;
        var last = new DateTime(Math.Clamp(ReadLong(doc, LastRegen), 0, now.Ticks), DateTimeKind.Utc);
        if (cur >= trueMax) last = now;
        else
        {
            var ticks = (long)((now - last).TotalSeconds / options.RegenIntervalSeconds);
            if (ticks > 0)
            {
                cur = (int)Math.Min(trueMax, cur + ticks * options.RegenPerInterval);
                last = cur >= trueMax ? now : last + TimeSpan.FromSeconds(ticks * options.RegenIntervalSeconds);
            }
        }
        doc[CurrentMax] = cur;
        doc[LastRegen] = last.Ticks;
    }

    static int ReadInt(JsonObject o, string k) => (int)Math.Clamp(ReadLong(o, k), int.MinValue, int.MaxValue);
    static long ReadLong(JsonObject o, string k)
    {
        try { return o[k] is JsonValue v ? (v.TryGetValue<long>(out var l) ? l : v.TryGetValue<int>(out var i) ? i : 0) : 0; }
        catch (InvalidOperationException) { return 0; }
    }
}
