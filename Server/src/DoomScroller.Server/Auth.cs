using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace DoomScroller.Server;

public interface IPlayerStore
{
    /// <summary>Returns the player id for this install, creating the player on first sight.</summary>
    Task<Guid> GetOrCreateByInstallAsync(Guid installId, CancellationToken ct);
}

public sealed class NpgsqlPlayerStore(NpgsqlDataSource db) : IPlayerStore
{
    public async Task<Guid> GetOrCreateByInstallAsync(Guid installId, CancellationToken ct)
    {
        // Atomic upsert: concurrent first calls for one install converge on one row.
        await using var cmd = db.CreateCommand(
            "INSERT INTO players (id, install_id) VALUES ($1, $2) " +
            "ON CONFLICT (install_id) DO UPDATE SET install_id = EXCLUDED.install_id RETURNING id");
        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue(installId);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }
}

public sealed class InMemoryPlayerStore : IPlayerStore
{
    readonly Dictionary<Guid, Guid> byInstall = new();
    public Task<Guid> GetOrCreateByInstallAsync(Guid installId, CancellationToken ct)
    {
        lock (byInstall)
        {
            if (!byInstall.TryGetValue(installId, out var id)) byInstall[installId] = id = Guid.NewGuid();
            return Task.FromResult(id);
        }
    }
}

/// <summary>Compact HS256 token: base64url(payload).base64url(hmac). Payload is {"sub":playerId,"exp":unixSeconds}.</summary>
public sealed class TokenService(string signingKey, TimeSpan lifetime)
{
    readonly byte[] key = Encoding.UTF8.GetBytes(signingKey);

    public string Issue(Guid playerId, DateTimeOffset now)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new { sub = playerId, exp = now.Add(lifetime).ToUnixTimeSeconds() });
        var body = B64(payload);
        return body + "." + B64(HMACSHA256.HashData(key, Encoding.ASCII.GetBytes(body)));
    }

    public Guid? Validate(string? token, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(token)) return null;
        var parts = token.Split('.');
        if (parts.Length != 2) return null;
        try
        {
            var expected = HMACSHA256.HashData(key, Encoding.ASCII.GetBytes(parts[0]));
            if (!CryptographicOperations.FixedTimeEquals(expected, Unb64(parts[1]))) return null;
            using var doc = JsonDocument.Parse(Unb64(parts[0]));
            if (doc.RootElement.GetProperty("exp").GetInt64() <= now.ToUnixTimeSeconds()) return null;
            return doc.RootElement.GetProperty("sub").GetGuid();
        }
        catch { return null; }
    }

    static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    static byte[] Unb64(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}

public static class AuthExtensions
{
    public const string PlayerIdItem = "PlayerId";

    /// <summary>Every path except /health and /auth/anonymous needs a valid bearer token (401 otherwise).</summary>
    public static void UseTokenAuth(this WebApplication app, TokenService? tokens)
    {
        app.Use(async (ctx, next) =>
        {
            var path = ctx.Request.Path;
            if (path.Equals("/health") || path.Equals("/auth/anonymous")) { await next(); return; }
            var h = ctx.Request.Headers.Authorization.ToString();
            var token = h.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? h[7..].Trim() : null;
            var player = tokens?.Validate(token, DateTimeOffset.UtcNow);
            if (player is null) { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return; }
            ctx.Items[PlayerIdItem] = player.Value;
            await next();
        });
    }
}

public sealed record AnonymousAuthRequest(string? InstallId);
