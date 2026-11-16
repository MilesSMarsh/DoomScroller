using System.Reflection;
using Npgsql;

namespace DoomScroller.Server;

public interface IDatabaseProbe
{
    Task<bool> IsReachableAsync(CancellationToken ct);
}

public sealed class NpgsqlProbe(NpgsqlDataSource? db) : IDatabaseProbe
{
    public async Task<bool> IsReachableAsync(CancellationToken ct)
    {
        if (db is null) return false;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            await using var cmd = db.CreateCommand("SELECT 1");
            await cmd.ExecuteScalarAsync(timeout.Token);
            return true;
        }
        catch { return false; }
    }
}

public static class Migrator
{
    /// <summary>Applies embedded Migrations/*.sql in name order, once each. Dev convenience; safe to re-run.</summary>
    public static async Task RunAsync(NpgsqlDataSource db, ILogger log, CancellationToken ct)
    {
        await using (var c = db.CreateCommand("CREATE TABLE IF NOT EXISTS migrations_applied (name text PRIMARY KEY, applied_utc timestamptz NOT NULL DEFAULT now())"))
            await c.ExecuteNonQueryAsync(ct);
        var asm = Assembly.GetExecutingAssembly();
        foreach (var res in asm.GetManifestResourceNames().Where(n => n.EndsWith(".sql")).OrderBy(n => n, StringComparer.Ordinal))
        {
            await using var check = db.CreateCommand("SELECT 1 FROM migrations_applied WHERE name = $1");
            check.Parameters.AddWithValue(res);
            if (await check.ExecuteScalarAsync(ct) is not null) continue;
            using var reader = new StreamReader(asm.GetManifestResourceStream(res)!);
            var sql = await reader.ReadToEndAsync(ct);
            await using var batch = db.CreateCommand(sql + ";\nINSERT INTO migrations_applied(name) VALUES ('" + res.Replace("'", "''") + "')");
            await batch.ExecuteNonQueryAsync(ct);
            log.LogInformation("Applied migration {Name}", res);
        }
    }
}

public sealed record FightRequest(string? FightId, string? EncounterId, bool ReviveArmed);

public partial class Program
{
    public static void Main(string[] args)
    {
        var app = Build(args);
        app.Run();
    }

    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        // All config from environment variables (DB_CONNECTION, PORT, ...). No secrets in files.
        var conn = builder.Configuration["DB_CONNECTION"];
        var port = builder.Configuration["PORT"];
        if (!string.IsNullOrEmpty(port)) builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

        var db = string.IsNullOrEmpty(conn) ? null : NpgsqlDataSource.Create(conn);
        builder.Services.AddSingleton<IDatabaseProbe>(new NpgsqlProbe(db));

        // Signing key from env (JWT_SIGNING_KEY). Without it no token can be issued or accepted.
        var signingKey = builder.Configuration["JWT_SIGNING_KEY"];
        var tokens = string.IsNullOrEmpty(signingKey) ? null : new TokenService(signingKey, TimeSpan.FromDays(30));
        if (db is not null)
        {
            builder.Services.AddSingleton<IPlayerStore>(new NpgsqlPlayerStore(db));
            builder.Services.AddSingleton<IProfileStore>(new NpgsqlProfileStore(db));
            builder.Services.AddSingleton<IFightStore>(new NpgsqlFightStore(db));
        }
        builder.Services.AddSingleton<ISeedSource, RandomSeedSource>();
        // Static data, read once. ENCOUNTERS_PATH overrides the copy shipped next to the binary.
        var encPath = builder.Configuration["ENCOUNTERS_PATH"] ?? Path.Combine(AppContext.BaseDirectory, "data", "encounters.json");
        builder.Services.AddSingleton(sp => EncounterCatalog.LoadFile(encPath));
        builder.Services.AddSingleton(ProfileOptions.From(builder.Configuration));
        builder.Services.AddSingleton(TimeProvider.System);

        var app = builder.Build();
        app.UseTokenAuth(tokens);

        app.MapPost("/auth/anonymous", async (AnonymousAuthRequest req, IServiceProvider sp, CancellationToken ct) =>
        {
            if (tokens is null) return Results.Problem("Signing key not configured.", statusCode: 503);
            var store = sp.GetService<IPlayerStore>();
            if (store is null) return Results.Problem("Database not configured.", statusCode: 503);
            if (!Guid.TryParse(req.InstallId, out var installId) || installId == Guid.Empty)
                return Results.BadRequest(new { error = "installId must be a GUID" });
            var playerId = await store.GetOrCreateByInstallAsync(installId, ct);
            app.Logger.LogInformation("Sign-in for player {PlayerId}", playerId);
            return Results.Ok(new { playerId, token = tokens.Issue(playerId, DateTimeOffset.UtcNow) });
        });

        // Smallest authenticated endpoint; lets clients verify a token.
        app.MapGet("/auth/me", (HttpContext c) => Results.Ok(new { playerId = c.Items[AuthExtensions.PlayerIdItem] }));

        // Profile: whole SaveData JSON per player. Responses wrap the document as a string so the
        // client can hand it to JsonUtility untouched. PUT carries the base revision in ?revision=.
        app.MapGet("/profile", async (HttpContext c, IServiceProvider sp, CancellationToken ct) =>
        {
            var svc = ProfileSvc(sp);
            if (svc is null) return Results.Problem("Database not configured.", statusCode: 503);
            return ToHttp(svc, await svc.GetAsync((Guid)c.Items[AuthExtensions.PlayerIdItem]!, ct));
        });
        app.MapPut("/profile", async (HttpContext c, IServiceProvider sp, CancellationToken ct) =>
        {
            var svc = ProfileSvc(sp);
            if (svc is null) return Results.Problem("Database not configured.", statusCode: 503);
            var max = sp.GetRequiredService<ProfileOptions>().MaxDocumentBytes;
            if (c.Request.ContentLength > max) return Results.BadRequest(new { error = "Document too large." });
            using var ms = new MemoryStream();
            var buf = new byte[8192];
            int n;
            while ((n = await c.Request.Body.ReadAsync(buf, ct)) > 0)
            {
                ms.Write(buf, 0, n);
                if (ms.Length > max) return Results.BadRequest(new { error = "Document too large." });
            }
            int? rev = int.TryParse(c.Request.Query["revision"], out var r) ? r : null;
            return ToHttp(svc, await svc.PutAsync((Guid)c.Items[AuthExtensions.PlayerIdItem]!, System.Text.Encoding.UTF8.GetString(ms.ToArray()), rev, ct));
        });

        // Fights: the server draws the seed, simulates, and commits before replying.
        app.MapPost("/fights", async (FightRequest req, HttpContext c, IServiceProvider sp, CancellationToken ct) =>
        {
            var svc = FightSvc(sp);
            if (svc is null) return Results.Problem("Database not configured.", statusCode: 503);
            var res = await svc.ResolveAsync((Guid)c.Items[AuthExtensions.PlayerIdItem]!, req.FightId, req.EncounterId, req.ReviveArmed, ct);
            return FightToHttp(sp, res);
        });
        app.MapGet("/fights/pending", async (HttpContext c, IServiceProvider sp, CancellationToken ct) =>
        {
            var svc = FightSvc(sp);
            if (svc is null) return Results.Problem("Database not configured.", statusCode: 503);
            var res = await svc.PendingAsync((Guid)c.Items[AuthExtensions.PlayerIdItem]!, ct);
            return res.Status == FightStatus.NotFound
                ? Results.Ok(new { hasPending = false, serverTimeUtc = DateTime.UtcNow })
                : FightToHttp(sp, res);
        });
        app.MapPost("/fights/{id}/ack", async (string id, HttpContext c, IServiceProvider sp, CancellationToken ct) =>
        {
            var svc = FightSvc(sp);
            if (svc is null) return Results.Problem("Database not configured.", statusCode: 503);
            return await svc.AcknowledgeAsync((Guid)c.Items[AuthExtensions.PlayerIdItem]!, id, ct) ? Results.NoContent() : Results.NotFound();
        });

        app.MapGet("/health", async (IDatabaseProbe probe, CancellationToken ct) => Results.Ok(new
        {
            version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0] ?? "unknown",
            serverTimeUtc = DateTime.UtcNow,
            databaseReachable = await probe.IsReachableAsync(ct),
        }));

        if (app.Configuration.GetValue("RUN_MIGRATIONS", false) && db is not null)
            app.Lifetime.ApplicationStarted.Register(() => _ = MigrateWithRetry(db!, app.Logger));
        return app;
    }

    static ProfileService? ProfileSvc(IServiceProvider sp) =>
        sp.GetService<IProfileStore>() is { } store
            ? new ProfileService(store, sp.GetRequiredService<ProfileOptions>(), sp.GetRequiredService<TimeProvider>())
            : null;

    static FightService? FightSvc(IServiceProvider sp) =>
        sp.GetService<IFightStore>() is { } fights && ProfileSvc(sp) is { } profileSvc
            ? new FightService(sp.GetRequiredService<IProfileStore>(), fights, sp.GetRequiredService<EncounterCatalog>(),
                sp.GetRequiredService<ISeedSource>(), profileSvc)
            : null;

    static IResult FightToHttp(IServiceProvider sp, FightOutcomeDto r)
    {
        var now = sp.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        return r.Status switch
        {
            FightStatus.Ok => Results.Ok(new
            {
                hasPending = true,
                fightId = r.Fight!.FightId,
                encounterId = r.Fight.EncounterId,
                seed = r.Fight.Seed,
                reviveFired = r.Fight.ReviveFired,
                revision = r.Revision,
                serverTimeUtc = now,
                result = System.Text.Json.Nodes.JsonNode.Parse(r.Fight.ResultJson),
            }),
            FightStatus.Conflict => Results.Json(new { error = r.Error, serverTimeUtc = now }, statusCode: 409),
            _ => Results.BadRequest(new { error = r.Error }),
        };
    }

    static IResult ToHttp(ProfileService svc, ProfileResult r) => r.Status switch
    {
        ProfileStatus.Ok => Results.Ok(new { isNew = r.Document is null, revision = r.Revision, serverTimeUtc = svc.Now, profile = r.Document }),
        ProfileStatus.Conflict => Results.Json(new { error = r.Error, revision = r.Revision, serverTimeUtc = svc.Now }, statusCode: 409),
        _ => Results.BadRequest(new { error = r.Error }),
    };

    static async Task MigrateWithRetry(NpgsqlDataSource db, ILogger log)
    {
        for (var i = 0; i < 30; i++)
        {
            try { await Migrator.RunAsync(db, log, CancellationToken.None); return; }
            catch (Exception e) { log.LogWarning("Migration attempt {N} failed: {Msg}", i + 1, e.Message); await Task.Delay(1000); }
        }
    }
}
