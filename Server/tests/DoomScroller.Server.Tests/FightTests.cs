using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DoomScroller.CombatCore;
using DoomScroller.Server;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Xunit.Abstractions;

public class FightTests(ITestOutputHelper output)
{
    sealed class FakeTime(DateTimeOffset start) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => start;
    }

    sealed class FixedSeed(int seed) : ISeedSource
    {
        public int Next() => seed;
    }

    sealed class CountingSeed : ISeedSource
    {
        public int Calls;
        public int Next() => ++Calls;
    }

    static readonly EncounterDef Fixture = new("Fixture", "Enemy", 90, 14, 0.9f, new[] { new DamageMultiplierInput { damageType = 2, multiplier = 1.5f } });
    static readonly EncounterDef Weak = new("Weak", "Enemy", 1, 1, 1f, new DamageMultiplierInput[0]);
    static readonly EncounterDef Brutal = new("Brutal", "Enemy", 1_000_000, 500, 1f, new DamageMultiplierInput[0]);
    static readonly EncounterDef Ambush = new("Ambush", "Ambush", 50, 5, 1f, new DamageMultiplierInput[0]);
    static EncounterCatalog Catalog => new(new[] { Fixture, Weak, Brutal, Ambush });

    // Same loadout as the Unity-side CombatCoreTests.MakeInput, expressed as a stored profile.
    static JsonObject Profile(int trueMax = 60, int current = 60, bool revive = false, int reviveLevel = 0)
    {
        var doc = JsonNode.Parse("""
        {"itemPool":[
          {"category":1,"slotType":4,"powerValue":9,"attackSpeed":1.2,"isRanged":false,"damageType":2,
           "attributeScalingEntries":[{"attribute":0,"coefficient":0.5}],
           "abilities":[{"abilityName":"Rend","powerBonus":4,"cooldownSeconds":3,
             "onCastEffects":[{"effectType":0,"magnitude":2,"durationSeconds":3,"tickIntervalSeconds":1}]}]},
          {"category":0,"slotType":1,"powerValue":2,
           "passiveEffects":[{"effectType":4,"magnitude":0.1,"tickIntervalSeconds":1,"scalesWithAttribute":false,"scalingAttribute":0,"scalingCoefficient":0}]},
          {"category":4,"potionEffect":1,"potionAmount":50}],
         "inventory":[0,1],"equipped":[{"slot":4,"itemIndex":0},{"slot":1,"itemIndex":1}],
         "reviveUpgrades":[],"attributeValues":[5,5,5,5],"gold":77,"autoConsumeRevivePotions":true}
        """)!.AsObject();
        doc["healthTrueMax"] = trueMax;
        doc["healthCurrentMax"] = current;
        doc["healthLastRegenUtcTicks"] = new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc).Ticks;
        if (revive)
        {
            doc["inventory"]!.AsArray().Add(2);
            if (reviveLevel > 0) doc["reviveUpgrades"]!.AsArray().Add(new JsonObject { ["itemIndex"] = 2, ["qualityLevel"] = reviveLevel });
        }
        return doc;
    }

    static FightInput Reference(int seed) => new()
    {
        seed = seed,
        player = new PlayerSideInput
        {
            startHp = 60, defense = 2, attributes = new[] { 5, 5, 5, 5 },
            weapons = new[] { new WeaponInput { power = 9, attackSpeed = 1.2f, damageType = 2, scaling = new[] { new ScalingInput { attribute = 0, coefficient = 0.5f } } } },
            abilities = new[]
            {
                new AbilityInput { name = "Rend", weaponIndex = 0, powerBonus = 4, cooldownSeconds = 3f,
                    onCastEffects = new[] { new EffectInput { effectType = StatusEffectType.DamageOverTime, magnitude = 2f, durationSeconds = 3f, tickIntervalSeconds = 1f } } }
            },
            passives = new[] { new PassiveInput { effectType = StatusEffectType.EmpowerDamage, magnitude = 0.1f, tickIntervalSeconds = 1f } }
        },
        enemy = new EnemySideInput { maxHp = 90, attackDamage = 14, attackSpeed = 0.9f, damageMultipliers = new[] { new DamageMultiplierInput { damageType = 2, multiplier = 1.5f } } }
    };

    static string Key(FightResult r) =>
        r.outcome + "|" + r.reviveFired + "|" + r.reviveBeatIndex + "|" + r.finalPlayerHp + "|" + r.finalEnemyHp + "|" +
        string.Join(";", r.beats.Select(b => $"{b.Actor},{b.IsRanged},{b.WasMiss},{b.DisplayDamage},{b.AbilityName},{b.IsDotTick}"));

    sealed class Rig
    {
        public readonly Guid Player = Guid.NewGuid();
        public readonly InMemoryProfileStore Profiles = new();
        public readonly InMemoryFightStore Fights;
        public readonly CountingSeed Seeds = new();
        public readonly FightService Service;
        public Rig(JsonObject doc)
        {
            Fights = new InMemoryFightStore(Profiles);
            var time = new FakeTime(new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero));
            Service = new FightService(Profiles, Fights, Catalog, Seeds, new ProfileService(Profiles, new ProfileOptions(), time));
            Profiles.CreateAsync(Player, doc.ToJsonString(), default).Wait();
        }
        public Task<FightOutcomeDto> Fight(string enc, bool armed = false, Guid? id = null) =>
            Service.ResolveAsync(Player, (id ?? Guid.NewGuid()).ToString(), enc, armed, default);
        public async Task<JsonObject> Doc() => (JsonObject)JsonNode.Parse((await Profiles.GetAsync(Player, default))!.Document)!;
    }

    [Fact]
    public void Server_built_input_matches_unity_fixture_result()
    {
        foreach (var seed in new[] { 1, 7, 42, 99991 })
        {
            var built = FightService.BuildInput(Profile(), Fixture, 60, seed, null);
            Assert.Equal(Key(CombatSimulator.Resolve(Reference(seed))), Key(CombatSimulator.Resolve(built)));
        }
    }

    [Fact]
    public async Task Stored_result_replays_the_stored_seed()
    {
        var rig = new Rig(Profile());
        var res = await rig.Fight("Fixture");
        Assert.Equal(FightStatus.Ok, res.Status);
        var stored = JsonSerializer.Deserialize<FightResult>(res.Fight!.ResultJson, new JsonSerializerOptions { IncludeFields = true })!;
        var expected = CombatSimulator.Resolve(Reference((int)res.Fight.Seed));
        Assert.Equal(Key(expected), Key(stored));
    }

    [Fact]
    public async Task Repeat_fight_id_returns_stored_result_without_rerolling()
    {
        var rig = new Rig(Profile(100, 100));
        var id = Guid.NewGuid();
        var first = await rig.Fight("Brutal", id: id);
        var again = await rig.Fight("Brutal", id: id);
        Assert.Equal(first.Fight!.ResultJson, again.Fight!.ResultJson);
        Assert.Equal(first.Fight.Seed, again.Fight.Seed);
        Assert.Equal(first.Revision, again.Revision);
        Assert.Equal(1, rig.Seeds.Calls);
    }

    [Fact]
    public async Task Loss_lowers_max_hp_once_and_win_does_not()
    {
        var rig = new Rig(Profile(100, 100));
        var id = Guid.NewGuid();
        await rig.Fight("Brutal", id: id);
        await rig.Fight("Brutal", id: id);
        Assert.Equal(75, (await rig.Doc())["healthCurrentMax"]!.GetValue<int>());
        await rig.Fight("Weak");
        Assert.Equal(75, (await rig.Doc())["healthCurrentMax"]!.GetValue<int>());
    }

    [Fact]
    public async Task Loss_never_drops_below_floor()
    {
        var rig = new Rig(Profile(100, 30));
        for (var i = 0; i < 3; i++) await rig.Fight("Brutal");
        Assert.Equal(20, (await rig.Doc())["healthCurrentMax"]!.GetValue<int>());
    }

    [Fact]
    public async Task Revive_consumed_only_when_it_fires()
    {
        var rig = new Rig(Profile(100, 100, revive: true, reviveLevel: 2));
        var win = await rig.Fight("Weak", armed: true);
        Assert.False(win.Fight!.ReviveFired);
        Assert.Contains(2, (await rig.Doc())["inventory"]!.AsArray().Select(n => n!.GetValue<int>()));

        var loss = await rig.Fight("Brutal", armed: true);
        Assert.True(loss.Fight!.ReviveFired);
        Assert.DoesNotContain(2, (await rig.Doc())["inventory"]!.AsArray().Select(n => n!.GetValue<int>()));
    }

    [Fact]
    public async Task Revive_stays_unarmed_when_client_flag_is_off_or_none_carried()
    {
        var rig = new Rig(Profile(100, 100, revive: true));
        Assert.False((await rig.Fight("Brutal", armed: false)).Fight!.ReviveFired);
        Assert.Contains(2, (await rig.Doc())["inventory"]!.AsArray().Select(n => n!.GetValue<int>()));

        var none = new Rig(Profile(100, 100));
        Assert.False((await none.Fight("Brutal", armed: true)).Fight!.ReviveFired);
    }

    [Fact]
    public async Task Unknown_or_non_fight_encounter_is_rejected_and_profile_untouched()
    {
        var rig = new Rig(Profile());
        var before = await rig.Profiles.GetAsync(rig.Player, default);
        foreach (var enc in new[] { "Nope", "Ambush" })
        {
            var res = await rig.Fight(enc);
            Assert.Equal(FightStatus.BadRequest, res.Status);
            Assert.False(string.IsNullOrEmpty(res.Error));
        }
        Assert.Equal(before, await rig.Profiles.GetAsync(rig.Player, default));
        Assert.Equal(0, rig.Seeds.Calls);
    }

    [Fact]
    public async Task One_thousand_fights_run_under_a_second()
    {
        var rig = new Rig(Profile(100, 100));
        await rig.Fight("Weak"); // warm up JIT
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 1000; i++) await rig.Fight("Weak");
        sw.Stop();
        output.WriteLine($"1000 fights: {sw.ElapsedMilliseconds} ms");
        Assert.True(sw.ElapsedMilliseconds < 1000, $"took {sw.ElapsedMilliseconds} ms");
    }

    // ---- HTTP ----

    sealed class Http
    {
        readonly WebApplicationFactory<Program> factory;
        public Http()
        {
            var profiles = new InMemoryProfileStore();
            var fights = new InMemoryFightStore(profiles);
            factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            {
                b.UseSetting("JWT_SIGNING_KEY", "test-signing-key-0123456789abcdef");
                b.ConfigureServices(s =>
                {
                    s.Replace(ServiceDescriptor.Singleton<IPlayerStore>(new InMemoryPlayerStore()));
                    s.Replace(ServiceDescriptor.Singleton<IProfileStore>(profiles));
                    s.Replace(ServiceDescriptor.Singleton<IFightStore>(fights));
                    s.Replace(ServiceDescriptor.Singleton<EncounterCatalog>(Catalog));
                    s.Replace(ServiceDescriptor.Singleton<ISeedSource>(new FixedSeed(42)));
                });
            });
        }
        public HttpClient Anonymous() => factory.CreateClient();
        public async Task<HttpClient> Player()
        {
            var c = factory.CreateClient();
            var res = await c.PostAsJsonAsync("/auth/anonymous", new { installId = Guid.NewGuid() });
            var j = await res.Content.ReadFromJsonAsync<JsonElement>();
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", j.GetProperty("token").GetString());
            return c;
        }
    }

    [Fact]
    public async Task Http_commit_pending_ack_flow()
    {
        var c = await new Http().Player();
        (await c.PutAsync("/profile?revision=0", new StringContent(Profile(100, 100).ToJsonString(), Encoding.UTF8, "application/json"))).EnsureSuccessStatusCode();

        var id = Guid.NewGuid();
        var post = await c.PostAsJsonAsync("/fights", new { fightId = id, encounterId = "Fixture", reviveArmed = false });
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        var j = await post.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(42, j.GetProperty("seed").GetInt32());
        Assert.True(j.GetProperty("revision").GetInt32() >= 2);
        Assert.True(j.GetProperty("result").GetProperty("beats").GetArrayLength() > 0);

        var pending = await (await c.GetAsync("/fights/pending")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(pending.GetProperty("hasPending").GetBoolean());
        Assert.Equal(id, pending.GetProperty("fightId").GetGuid());
        Assert.Equal(j.GetProperty("result").ToString(), pending.GetProperty("result").ToString());

        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync($"/fights/{id}/ack", null)).StatusCode);
        var after = await (await c.GetAsync("/fights/pending")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(after.GetProperty("hasPending").GetBoolean());
    }

    [Fact]
    public async Task Http_bad_encounter_is_400_with_reason_and_needs_auth()
    {
        var env = new Http();
        var c = await env.Player();
        (await c.PutAsync("/profile?revision=0", new StringContent(Profile().ToJsonString(), Encoding.UTF8, "application/json"))).EnsureSuccessStatusCode();
        var res = await c.PostAsJsonAsync("/fights", new { fightId = Guid.NewGuid(), encounterId = "Ambush" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.False(string.IsNullOrEmpty((await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString()));
        Assert.Equal(HttpStatusCode.Unauthorized, (await env.Anonymous().GetAsync("/fights/pending")).StatusCode);
    }
}
