using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DoomScroller.Server;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

public class AuthTests
{
    const string Key = "test-signing-key-0123456789abcdef";

    static HttpClient Client(string? key = Key, InMemoryPlayerStore? store = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            if (key != null) b.UseSetting("JWT_SIGNING_KEY", key);
            b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<IPlayerStore>(store ?? new InMemoryPlayerStore())));
        }).CreateClient();

    static async Task<(Guid player, string token)> SignIn(HttpClient c, Guid install)
    {
        var res = await c.PostAsJsonAsync("/auth/anonymous", new { installId = install });
        res.EnsureSuccessStatusCode();
        var j = await res.Content.ReadFromJsonAsync<JsonElement>();
        return (j.GetProperty("playerId").GetGuid(), j.GetProperty("token").GetString()!);
    }

    [Fact]
    public async Task New_install_gets_new_player_id()
    {
        var c = Client();
        var (a, _) = await SignIn(c, Guid.NewGuid());
        var (b, _) = await SignIn(c, Guid.NewGuid());
        Assert.NotEqual(a, b);
    }

    [Fact]
    public async Task Same_install_returns_same_player()
    {
        var c = Client();
        var install = Guid.NewGuid();
        var (a, _) = await SignIn(c, install);
        var (b, _) = await SignIn(c, install);
        Assert.Equal(a, b);
    }

    [Fact]
    public async Task Valid_token_reaches_protected_endpoint()
    {
        var c = Client();
        var (player, token) = await SignIn(c, Guid.NewGuid());
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var j = await c.GetFromJsonAsync<JsonElement>("/auth/me");
        Assert.Equal(player, j.GetProperty("playerId").GetGuid());
    }

    [Fact]
    public async Task Missing_token_is_401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client().GetAsync("/auth/me")).StatusCode);

    [Theory]
    [InlineData("garbage")]
    [InlineData("a.b")]
    public async Task Bad_token_is_401(string token)
    {
        var c = Client();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Token_signed_with_other_key_or_expired_is_401()
    {
        var c = Client();
        var id = Guid.NewGuid();
        foreach (var token in new[]
        {
            new TokenService("some-other-key-0123456789abcdef", TimeSpan.FromDays(1)).Issue(id, DateTimeOffset.UtcNow),
            new TokenService(Key, TimeSpan.FromDays(1)).Issue(id, DateTimeOffset.UtcNow.AddDays(-2)),
        })
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            Assert.Equal(HttpStatusCode.Unauthorized, (await c.SendAsync(req)).StatusCode);
        }
    }

    [Fact]
    public async Task Health_needs_no_token() =>
        Assert.Equal(HttpStatusCode.OK, (await Client().GetAsync("/health")).StatusCode);

    [Fact]
    public async Task Invalid_install_id_is_400()
    {
        var res = await Client().PostAsJsonAsync("/auth/anonymous", new { installId = "nope" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
