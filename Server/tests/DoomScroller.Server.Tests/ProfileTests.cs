using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DoomScroller.Server;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

public class ProfileTests
{
    sealed class FakeTime(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now = start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    sealed class Env
    {
        public readonly FakeTime Time = new(new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero));
        readonly WebApplicationFactory<Program> factory;
        public Env()
        {
            var players = new InMemoryPlayerStore();
            var profiles = new InMemoryProfileStore();
            factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            {
                b.UseSetting("JWT_SIGNING_KEY", "test-signing-key-0123456789abcdef");
                b.UseSetting("PROFILE_MAX_BYTES", "5000");
                b.ConfigureServices(s =>
                {
                    s.Replace(ServiceDescriptor.Singleton<IPlayerStore>(players));
                    s.Replace(ServiceDescriptor.Singleton<IProfileStore>(profiles));
                    s.Replace(ServiceDescriptor.Singleton<TimeProvider>(Time));
                });
            });
        }
        public async Task<HttpClient> Player()
        {
            var c = factory.CreateClient();
            var res = await c.PostAsJsonAsync("/auth/anonymous", new { installId = Guid.NewGuid() });
            var j = await res.Content.ReadFromJsonAsync<JsonElement>();
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", j.GetProperty("token").GetString());
            return c;
        }
    }

    static readonly string Sample = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "savedata_sample.json"));

    static string With(string json, int? trueMax = null, int? current = null)
    {
        var o = JsonNode.Parse(json)!.AsObject();
        if (trueMax != null) o["healthTrueMax"] = trueMax;
        if (current != null) o["healthCurrentMax"] = current;
        return o.ToJsonString();
    }

    static Task<HttpResponseMessage> Put(HttpClient c, string body, int revision) =>
        c.PutAsync($"/profile?revision={revision}", new StringContent(body, Encoding.UTF8, "application/json"));

    static async Task<(int revision, JsonObject doc, bool isNew)> Read(HttpResponseMessage res)
    {
        res.EnsureSuccessStatusCode();
        var j = await res.Content.ReadFromJsonAsync<JsonElement>();
        var p = j.GetProperty("profile");
        return (j.GetProperty("revision").GetInt32(), p.ValueKind == JsonValueKind.Null ? null! : JsonNode.Parse(p.GetString()!)!.AsObject(), j.GetProperty("isNew").GetBoolean());
    }

    [Fact]
    public async Task First_get_is_new_marker_with_server_time()
    {
        var c = await new Env().Player();
        var res = await c.GetAsync("/profile");
        var j = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(j.GetProperty("isNew").GetBoolean());
        Assert.Equal(JsonValueKind.Null, j.GetProperty("profile").ValueKind);
        Assert.True(j.TryGetProperty("serverTimeUtc", out _));
    }

    [Fact]
    public async Task Round_trips_real_save_json()
    {
        var c = await new Env().Player();
        var (rev, _, _) = await Read(await Put(c, Sample, 0));
        Assert.Equal(1, rev);
        var (_, doc, isNew) = await Read(await c.GetAsync("/profile"));
        Assert.False(isNew);
        var expected = JsonNode.Parse(Sample)!.AsObject();
        foreach (var kv in expected)
        {
            if (kv.Key == "healthLastRegenUtcTicks") continue;
            Assert.True(JsonNode.DeepEquals(kv.Value, doc[kv.Key]), kv.Key);
        }
    }

    [Fact]
    public async Task Stale_revision_is_409()
    {
        var c = await new Env().Player();
        await Put(c, Sample, 0);
        Assert.Equal(HttpStatusCode.OK, (await Put(c, Sample, 1)).StatusCode);   // -> rev 2
        Assert.Equal(HttpStatusCode.Conflict, (await Put(c, Sample, 1)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Put(c, Sample, 0)).StatusCode);
    }

    [Fact]
    public async Task Player_cannot_read_another_players_profile()
    {
        var env = new Env();
        var a = await env.Player();
        var b = await env.Player();
        await Put(a, With(Sample, current: 77), 0);
        var (_, doc, isNew) = await Read(await b.GetAsync("/profile"));
        Assert.True(isNew);
        Assert.Null(doc);
    }

    [Fact]
    public async Task Upload_cannot_raise_current_max()
    {
        var env = new Env();
        var c = await env.Player();
        await Put(c, Sample, 0);                                  // current 60 of 100
        var (_, doc, _) = await Read(await Put(c, With(Sample, current: 100), 1));
        Assert.Equal(60, doc["healthCurrentMax"]!.GetValue<int>());
    }

    [Fact]
    public async Task First_sign_in_takes_client_value_clamped_to_true_max()
    {
        var c = await new Env().Player();
        var (_, doc, _) = await Read(await Put(c, With(Sample, trueMax: 100, current: 500), 0));
        Assert.Equal(100, doc["healthCurrentMax"]!.GetValue<int>());
    }

    [Fact]
    public async Task Regen_applies_server_time_and_caps_at_true_max()
    {
        var env = new Env();
        var c = await env.Player();
        await Put(c, Sample, 0);                                  // 60/100, clock = now
        env.Time.Now += TimeSpan.FromSeconds(95);                 // 3 whole ticks of 30 s = +30
        var (_, doc, _) = await Read(await Put(c, With(Sample, current: 1), 1));
        Assert.Equal(90, doc["healthCurrentMax"]!.GetValue<int>());
        Assert.Equal(env.Time.Now.UtcDateTime.Ticks - TimeSpan.FromSeconds(5).Ticks, doc["healthLastRegenUtcTicks"]!.GetValue<long>());
        env.Time.Now += TimeSpan.FromHours(1);
        var (_, capped, _) = await Read(await c.GetAsync("/profile"));
        Assert.Equal(100, capped["healthCurrentMax"]!.GetValue<int>());
    }

    [Fact]
    public async Task Client_regen_clock_is_ignored()
    {
        var env = new Env();
        var c = await env.Player();
        await Put(c, Sample, 0);
        var forged = JsonNode.Parse(Sample)!.AsObject();
        forged["healthLastRegenUtcTicks"] = 1L;
        var (_, doc, _) = await Read(await Put(c, forged.ToJsonString(), 1));
        Assert.Equal(env.Time.Now.UtcDateTime.Ticks, doc["healthLastRegenUtcTicks"]!.GetValue<long>());
        Assert.Equal(60, doc["healthCurrentMax"]!.GetValue<int>());
    }

    [Fact]
    public async Task Floor_is_twenty_percent_of_true_max()
    {
        var c = await new Env().Player();
        var (_, doc, _) = await Read(await Put(c, With(Sample, trueMax: 100, current: 5), 0));
        // First sign-in takes the client value; a later write floors it.
        var (_, after, _) = await Read(await Put(c, With(Sample, trueMax: 100), 1));
        Assert.True(after["healthCurrentMax"]!.GetValue<int>() >= 20);
    }

    [Fact]
    public async Task Oversized_and_malformed_bodies_are_400()
    {
        var c = await new Env().Player();
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(c, "{not json", 0)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(c, "[1,2]", 0)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(c, "{\"pad\":\"" + new string('x', 6000) + "\"}", 0)).StatusCode);
    }

    [Fact]
    public async Task Profile_needs_token()
    {
        var env = new Env();
        var c = await env.Player();
        c.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/profile")).StatusCode);
    }
}
