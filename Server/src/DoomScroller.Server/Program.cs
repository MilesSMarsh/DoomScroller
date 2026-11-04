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

        var app = builder.Build();

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

    static async Task MigrateWithRetry(NpgsqlDataSource db, ILogger log)
    {
        for (var i = 0; i < 30; i++)
        {
            try { await Migrator.RunAsync(db, log, CancellationToken.None); return; }
            catch (Exception e) { log.LogWarning("Migration attempt {N} failed: {Msg}", i + 1, e.Message); await Task.Delay(1000); }
        }
    }
}
