using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DoomScroller.Combat;
using DoomScroller.CombatCore;
using DoomScroller.Inventory;
using DoomScroller.Network;
using DoomScroller.Progression;
using NUnit.Framework;

namespace DoomScroller.Tests.EditMode
{
    // Acceptance coverage for 016 on the Unity side: the CommitFight/GetPendingFight client
    // surface beyond the happy path, the shared LoadoutMapper against FightInputBuilder (the
    // server builds FightInput through the mapper), Revive firing rules in CombatCore, and
    // the defeat max-HP rule the server mirrors from PlayerHealth.
    public class FightResolutionQaTests
    {
        static readonly ItemData[] NoItems = new ItemData[0];

        class FakeStore : IServerCredentialStore
        {
            public string InstallId { get; set; }
            public string Token { get; set; }
            public string PlayerId { get; set; }
        }

        class FakeTransport : IServerTransport
        {
            public readonly List<string> Urls = new List<string>();
            public readonly List<string> Bodies = new List<string>();
            public Func<string, string, string, string, ServerResponse> Handler;
            public Task<ServerResponse> SendAsync(string method, string url, string body, string token, TimeSpan timeout)
            {
                Urls.Add(url);
                Bodies.Add(body);
                return Task.FromResult(Handler(method, url, body, token));
            }
        }

        static ServerClient Make(IServerTransport t, FakeStore s) =>
            new ServerClient(t, s, "http://x/", TimeSpan.FromSeconds(1));

        // ---- Client surface ----

        [Test]
        public void CommitFight_Conflict409_IsNotOk_CarriesError_AndNoResult()
        {
            var t = new FakeTransport
            {
                Handler = (m, u, b, k) => new ServerResponse(409, "{\"error\":\"Profile changed during the fight; retry.\",\"serverTimeUtc\":\"t\"}")
            };
            var r = Make(t, new FakeStore { Token = "t" }).CommitFightAsync("f1", "Enemy_X", false).Result;
            Assert.AreEqual(409, r.Status);
            Assert.IsFalse(r.Ok);
            Assert.IsFalse(r.HasPending);
            Assert.IsNull(r.Result);
            Assert.AreEqual("Profile changed during the fight; retry.", r.Error);
        }

        [Test]
        public void CommitFight_RevivesFlagFalse_IsSentAsFalse()
        {
            var t = new FakeTransport { Handler = (m, u, b, k) => new ServerResponse(200, "{}") };
            Make(t, new FakeStore { Token = "t" }).CommitFightAsync("f1", "Enemy_X", false).Wait();
            StringAssert.Contains("\"reviveArmed\":false", t.Bodies[0]);
        }

        [Test]
        public void CommitFight_NonJsonBodyOn200_IsNotOk_NotTreatedAsCommitted()
        {
            var t = new FakeTransport { Handler = (m, u, b, k) => new ServerResponse(200, "<html>proxy</html>") };
            var r = Make(t, new FakeStore { Token = "t" }).CommitFightAsync("f1", "Enemy_X", false).Result;
            Assert.IsFalse(r.Ok);
            Assert.IsFalse(r.HasPending);
            Assert.IsNull(r.Result);
        }

        [Test]
        public void GetPendingFight_Unauthorized_SurfacesStatus_NoPending()
        {
            var t = new FakeTransport
            {
                Handler = (m, u, b, k) => u.EndsWith("/auth/anonymous")
                    ? new ServerResponse(200, "{\"playerId\":\"p1\",\"token\":\"tok\"}")
                    : new ServerResponse(401, "")
            };
            var r = Make(t, new FakeStore { Token = "stale" }).GetPendingFightAsync().Result;
            Assert.AreEqual(401, r.Status);
            Assert.IsFalse(r.Ok);
            Assert.IsFalse(r.HasPending);
        }

        [Test]
        public void AcknowledgeFight_ServerNotFound_ReturnsFalse()
        {
            var t = new FakeTransport { Handler = (m, u, b, k) => new ServerResponse(404, "") };
            Assert.IsFalse(Make(t, new FakeStore { Token = "t" }).AcknowledgeFightAsync("nope").Result);
        }

        [Test]
        public void AcknowledgeFight_EscapesFightIdInPath()
        {
            var t = new FakeTransport { Handler = (m, u, b, k) => new ServerResponse(204, "") };
            Assert.IsTrue(Make(t, new FakeStore { Token = "t" }).AcknowledgeFightAsync("a/b c").Result);
            Assert.AreEqual("http://x/fights/a%2Fb%20c/ack", t.Urls[0]);
        }

        [Test]
        public void FightResponse_OkBoundaries()
        {
            Assert.IsFalse(new FightResponse(199, false, null, null, 0, false, 0, null, null).Ok);
            Assert.IsTrue(new FightResponse(200, false, null, null, 0, false, 0, null, null).Ok);
            Assert.IsTrue(new FightResponse(299, false, null, null, 0, false, 0, null, null).Ok);
            Assert.IsFalse(new FightResponse(300, false, null, null, 0, false, 0, null, null).Ok);
            Assert.IsFalse(new FightResponse(0, false, null, null, 0, false, 0, null, null).Ok);
        }

        // A transport-less interface implementer that only supplies the required members must
        // get the safe defaults: no fight, no ack, never a fake success.
        class BareClient : IServerClient
        {
            public bool IsSignedIn => false;
            public string PlayerId => null;
            public Task<bool> SignInAsync() => Task.FromResult(false);
            public Task<ServerResponse> SendAsync(string method, string path, string jsonBody = null) =>
                Task.FromResult(new ServerResponse(0, null));
            public Task<ProfileResponse> LoadProfileAsync() => Task.FromResult(new ProfileResponse(0, false, null, 0, null));
            public Task<ProfileResponse> SaveProfileAsync(string saveJson, int baseRevision) =>
                Task.FromResult(new ProfileResponse(0, false, null, 0, null));
        }

        [Test]
        public void IServerClient_FightDefaults_AreSafeNoOps()
        {
            IServerClient c = new BareClient();
            var commit = c.CommitFightAsync("f1", "Enemy_X", true).Result;
            Assert.AreEqual(0, commit.Status);
            Assert.IsFalse(commit.Ok);
            Assert.IsFalse(commit.HasPending);
            var pending = c.GetPendingFightAsync().Result;
            Assert.IsFalse(pending.HasPending);
            Assert.IsFalse(c.AcknowledgeFightAsync("f1").Result);
        }

        // ---- Shared item mapping: LoadoutMapper (server path) must equal FightInputBuilder (client path) ----

        static ScalingInput[] ToScaling(ItemData item) =>
            (item.attributeScalingEntries ?? new AttributeScaling[0])
                .Select(e => new ScalingInput { attribute = (int)e.attribute, coefficient = e.coefficient }).ToArray();

        static EffectInput[] ToEffects(StatusEffectData[] effects) =>
            (effects ?? new StatusEffectData[0]).Select(e => new EffectInput
            {
                effectType = e.effectType, magnitude = e.magnitude, durationSeconds = e.durationSeconds,
                tickIntervalSeconds = e.tickIntervalSeconds
            }).ToArray();

        // What the server stores per item in the profile snapshot, expressed as LoadoutItem.
        static LoadoutItem ToLoadoutItem(ItemData item) => new LoadoutItem
        {
            slot = (int)item.slotType,
            category = (int)item.category,
            powerValue = item.powerValue,
            attackSpeed = item.attackSpeed,
            isRanged = item.isRanged,
            damageType = (int)item.damageType,
            scaling = ToScaling(item),
            abilities = (item.abilities ?? new WeaponAbilityData[0]).Select(a => new LoadoutAbility
            {
                name = a.abilityName, powerBonus = a.powerBonus, cooldownSeconds = a.cooldownSeconds,
                onCastEffects = ToEffects(a.onCastEffects)
            }).ToArray(),
            passives = (item.passiveEffects ?? new StatusEffectData[0]).Select(p => new PassiveInput
            {
                effectType = p.effectType, magnitude = p.magnitude, tickIntervalSeconds = p.tickIntervalSeconds,
                scalesWithAttribute = p.scalesWithAttribute, scalingAttribute = (int)p.scalingAttribute,
                scalingCoefficient = p.scalingCoefficient
            }).ToArray()
        };

        static ItemData[] EquippedSet()
        {
            var ability = new WeaponAbilityData
            {
                abilityName = "Cleave", powerBonus = 3, cooldownSeconds = 2f,
                onCastEffects = new[] { new StatusEffectData { effectType = StatusEffectType.DamageOverTime, magnitude = 2f, durationSeconds = 4f, tickIntervalSeconds = 1f } }
            };
            var main = TestFixtures.MakeWeapon(power: 8, attackSpeed: 1.5f, damageType: DamageType.Arcane,
                slot: EquipmentSlotType.RightHand, abilities: new[] { ability },
                scaling: new[] { new AttributeScaling { attribute = PlayerAttribute.Spirit, coefficient = 0.5f } },
                passives: new[] { new StatusEffectData { effectType = StatusEffectType.EmpowerDamage, magnitude = 0.2f } });
            var off = TestFixtures.MakeWeapon(power: 4, attackSpeed: 1f, damageType: DamageType.Blunt,
                slot: EquipmentSlotType.LeftHand, isTwoHanded: false);
            var chest = TestFixtures.MakeGear(ItemCategory.Armor, EquipmentSlotType.Chest, power: 4);
            var talisman = TestFixtures.MakeGear(ItemCategory.Talisman, EquipmentSlotType.Talisman1, power: 2,
                passives: new[] { new StatusEffectData { effectType = StatusEffectType.ArmorUp, magnitude = 1f } });
            return new[] { main, off, chest, talisman };
        }

        static void AssertPlayersEqual(PlayerSideInput expected, PlayerSideInput actual)
        {
            Assert.AreEqual(expected.startHp, actual.startHp, "startHp");
            Assert.AreEqual(expected.defense, actual.defense, "defense");
            CollectionAssert.AreEqual(expected.attributes, actual.attributes, "attributes");

            Assert.AreEqual(expected.weapons.Length, actual.weapons.Length, "weapon count");
            for (var i = 0; i < expected.weapons.Length; i++)
            {
                var e = expected.weapons[i];
                var a = actual.weapons[i];
                Assert.AreEqual(e.power, a.power, $"weapon[{i}].power");
                Assert.AreEqual(e.attackSpeed, a.attackSpeed, $"weapon[{i}].attackSpeed");
                Assert.AreEqual(e.isRanged, a.isRanged, $"weapon[{i}].isRanged");
                Assert.AreEqual(e.damageType, a.damageType, $"weapon[{i}].damageType");
                Assert.AreEqual(e.scaling.Length, a.scaling.Length, $"weapon[{i}].scaling count");
                for (var s = 0; s < e.scaling.Length; s++)
                {
                    Assert.AreEqual(e.scaling[s].attribute, a.scaling[s].attribute);
                    Assert.AreEqual(e.scaling[s].coefficient, a.scaling[s].coefficient);
                }
            }

            Assert.AreEqual(expected.abilities.Length, actual.abilities.Length, "ability count");
            for (var i = 0; i < expected.abilities.Length; i++)
            {
                var e = expected.abilities[i];
                var a = actual.abilities[i];
                Assert.AreEqual(e.name, a.name, "ability name");
                Assert.AreEqual(e.weaponIndex, a.weaponIndex, "ability weaponIndex");
                Assert.AreEqual(e.powerBonus, a.powerBonus, "ability powerBonus");
                Assert.AreEqual(e.cooldownSeconds, a.cooldownSeconds, "ability cooldown");
                Assert.AreEqual(e.onCastEffects.Length, a.onCastEffects.Length, "ability effects count");
                for (var k = 0; k < e.onCastEffects.Length; k++)
                {
                    Assert.AreEqual(e.onCastEffects[k].effectType, a.onCastEffects[k].effectType);
                    Assert.AreEqual(e.onCastEffects[k].magnitude, a.onCastEffects[k].magnitude);
                    Assert.AreEqual(e.onCastEffects[k].durationSeconds, a.onCastEffects[k].durationSeconds);
                    Assert.AreEqual(e.onCastEffects[k].tickIntervalSeconds, a.onCastEffects[k].tickIntervalSeconds);
                }
            }

            Assert.AreEqual(expected.passives.Length, actual.passives.Length, "passive count");
            for (var i = 0; i < expected.passives.Length; i++)
            {
                var e = expected.passives[i];
                var a = actual.passives[i];
                Assert.AreEqual(e.effectType, a.effectType, "passive effectType");
                Assert.AreEqual(e.magnitude, a.magnitude, "passive magnitude");
                Assert.AreEqual(e.tickIntervalSeconds, a.tickIntervalSeconds, "passive tick");
                Assert.AreEqual(e.scalesWithAttribute, a.scalesWithAttribute, "passive scales");
                Assert.AreEqual(e.scalingAttribute, a.scalingAttribute, "passive scalingAttribute");
                Assert.AreEqual(e.scalingCoefficient, a.scalingCoefficient, "passive scalingCoefficient");
            }
        }

        [Test]
        public void LoadoutMapper_MatchesFightInputBuilder_ForTheSameEquipment()
        {
            var items = EquippedSet();
            var loadout = TestFixtures.MakeLoadout(items);
            var attributes = new PlayerAttributes();
            var attrValues = new[]
            {
                attributes.Get(PlayerAttribute.Constitution), attributes.Get(PlayerAttribute.Agility),
                attributes.Get(PlayerAttribute.Spirit), attributes.Get(PlayerAttribute.Expertise)
            };

            var fromBuilder = FightInputBuilder.BuildPlayer(loadout, attributes, 60);
            var fromMapper = LoadoutMapper.BuildPlayer(items.Select(ToLoadoutItem), attrValues, 60);

            AssertPlayersEqual(fromBuilder, fromMapper);
            Assert.AreEqual(4 + 2, fromMapper.defense, "chest 4 + talisman 2 (weapons never count as defense)");
            Assert.AreEqual(2, fromMapper.weapons.Length);
            Assert.AreEqual(1, fromMapper.abilities[0].weaponIndex, "hand weapons index LeftHand first, so the right-hand weapon is 1");
        }

        [Test]
        public void LoadoutMapper_SameFight_ResolvesIdenticallyToBuilderPath()
        {
            var items = EquippedSet();
            var loadout = TestFixtures.MakeLoadout(items);
            var attributes = new PlayerAttributes();
            var attrValues = new[] { attributes.Get(PlayerAttribute.Constitution), attributes.Get(PlayerAttribute.Agility),
                attributes.Get(PlayerAttribute.Spirit), attributes.Get(PlayerAttribute.Expertise) };
            var enemy = new EnemySideInput { maxHp = 120, attackDamage = 7, attackSpeed = 0.9f, canAttack = true };

            var clientInput = new FightInput
            {
                player = FightInputBuilder.BuildPlayer(loadout, attributes, 60),
                enemy = enemy,
                seed = 20261115
            };
            var serverInput = new FightInput
            {
                player = LoadoutMapper.BuildPlayer(items.Select(ToLoadoutItem), attrValues, 60),
                enemy = enemy,
                seed = 20261115
            };

            var a = CombatSimulator.Resolve(clientInput);
            var b = CombatSimulator.Resolve(serverInput);
            Assert.AreEqual(a.outcome, b.outcome);
            Assert.AreEqual(a.finalPlayerHp, b.finalPlayerHp);
            Assert.AreEqual(a.finalEnemyHp, b.finalEnemyHp);
            Assert.AreEqual(a.beats.Length, b.beats.Length);
            for (var i = 0; i < a.beats.Length; i++)
            {
                Assert.AreEqual(a.beats[i].DisplayDamage, b.beats[i].DisplayDamage, $"beat {i} damage");
                Assert.AreEqual(a.beats[i].WasMiss, b.beats[i].WasMiss, $"beat {i} miss");
                Assert.AreEqual(a.beats[i].AbilityName, b.beats[i].AbilityName, $"beat {i} ability");
            }
        }

        [Test]
        public void LoadoutMapper_IgnoresNullAndOutOfRangeSlots()
        {
            var stray = new[]
            {
                null,
                new LoadoutItem { slot = -1, category = 0, powerValue = 50 },
                new LoadoutItem { slot = LoadoutMapper.SlotCount, category = 0, powerValue = 50 },
            };
            var p = LoadoutMapper.BuildPlayer(stray, new int[0], 10);
            Assert.AreEqual(0, p.defense);
            Assert.AreEqual(0, p.weapons.Length);
            Assert.AreEqual(10, p.startHp);
        }

        [Test]
        public void LoadoutMapper_WeaponOutsideHandSlots_IsNotAWeapon()
        {
            var headWeapon = new LoadoutItem { slot = 0, category = LoadoutMapper.WeaponCategory, powerValue = 9 };
            var p = LoadoutMapper.BuildPlayer(new[] { headWeapon }, new int[0], 10);
            Assert.AreEqual(0, p.weapons.Length);
            Assert.AreEqual(0, p.defense);
        }

        [Test]
        public void LoadoutMapper_Attributes_MissingReadZero_ExtraIgnored()
        {
            Assert.AreEqual(new[] { 5, 0, 0, 0 }, LoadoutMapper.BuildPlayer(new LoadoutItem[0], new[] { 5 }, 1).attributes);
            Assert.AreEqual(new[] { 0, 0, 0, 0 }, LoadoutMapper.BuildPlayer(new LoadoutItem[0], null, 1).attributes);
            Assert.AreEqual(new[] { 1, 2, 3, 4 }, LoadoutMapper.BuildPlayer(new LoadoutItem[0], new[] { 1, 2, 3, 4, 99 }, 1).attributes);
        }

        // ---- Revive firing rules (the server consumes a Revive only when CombatCore reports it fired) ----

        [Test]
        public void Revive_NotFired_WhenPlayerWinsFirst()
        {
            var weapon = TestFixtures.MakeWeapon(power: 50, attackSpeed: 1f);
            var loadout = TestFixtures.MakeLoadout(weapon);
            var sim = TestFixtures.MakeSim(loadout, new PlayerAttributes(), 20, 10, 0, 1f, null,
                new FixedRandom(), enemyCanAttack: true, reviveArmedPercent: 50);
            sim.RunUntilDeathOrVictory();
            Assert.IsTrue(sim.IsEnemyDead);
            Assert.IsFalse(sim.ReviveFired);
            Assert.AreEqual(-1, sim.ReviveBeatIndex);
        }

        [Test]
        public void Revive_Fires_OnLethalHit_WhenArmed()
        {
            var weapon = TestFixtures.MakeWeapon(power: 1, attackSpeed: 1f);
            var loadout = TestFixtures.MakeLoadout(weapon);
            var sim = TestFixtures.MakeSim(loadout, new PlayerAttributes(), 10, 1000, 100, 1f, null,
                new FixedRandom(), enemyCanAttack: true, reviveArmedPercent: 50);
            sim.RunUntilDeathOrVictory();
            Assert.IsTrue(sim.ReviveFired);
            Assert.GreaterOrEqual(sim.ReviveBeatIndex, 0);
        }

        [Test]
        public void Revive_NotFired_WhenNotArmed_EvenOnLethalHit()
        {
            var weapon = TestFixtures.MakeWeapon(power: 1, attackSpeed: 1f);
            var loadout = TestFixtures.MakeLoadout(weapon);
            var sim = TestFixtures.MakeSim(loadout, new PlayerAttributes(), 10, 1000, 100, 1f, null,
                new FixedRandom(), enemyCanAttack: true, reviveArmedPercent: 0);
            sim.RunUntilDeathOrVictory();
            Assert.IsTrue(sim.IsPlayerDead);
            Assert.IsFalse(sim.ReviveFired);
        }

        [Test]
        public void Resolve_SameInputAndSeed_IsDeterministic()
        {
            var weapon = TestFixtures.MakeWeapon(power: 6, attackSpeed: 1.2f);
            var loadout = TestFixtures.MakeLoadout(weapon);
            var input = FightInputBuilder.Build(loadout, new PlayerAttributes(), null, 40, 777, 90, 6, 1.1f, null, true);
            var first = CombatSimulator.Resolve(input);
            var second = CombatSimulator.Resolve(input);
            Assert.AreEqual(first.outcome, second.outcome);
            Assert.AreEqual(first.beats.Length, second.beats.Length);
            Assert.AreEqual(first.finalPlayerHp, second.finalPlayerHp);
            Assert.AreEqual(first.finalEnemyHp, second.finalEnemyHp);
        }

        // ---- Defeat max-HP rule (the server applies the same rule to the stored profile) ----

        // Pinned values: 25% of true max rounded like Mathf.RoundToInt (half to even), floored at
        // max(1, round(20% of true max)). The server's ApplyDefeatPenalty must produce the same numbers.
        [TestCase(10, 8)]
        [TestCase(30, 22)]
        [TestCase(50, 38)]
        [TestCase(101, 76)]
        public void DefeatPenalty_LowersCurrentMaxOnce_ByRoundedQuarter(int trueMax, int expectedAfterOneLoss)
        {
            var health = new PlayerHealth(trueMax, 1, 60f);
            health.ApplyDefeatPenalty();
            Assert.AreEqual(expectedAfterOneLoss, health.CurrentMaxHealth);
            Assert.AreEqual(trueMax, health.TrueMaxHealth, "true max is never lowered by a defeat");
        }

        [Test]
        public void DefeatPenalty_NeverBelowFloor_AndFloorIsStable()
        {
            var health = new PlayerHealth(10, 1, 60f);
            for (var i = 0; i < 10; i++) health.ApplyDefeatPenalty();
            Assert.AreEqual(health.Floor, health.CurrentMaxHealth);
            Assert.AreEqual(2, health.Floor);
            Assert.IsTrue(health.IsAtFloor);
        }
    }
}
