using System;
using System.Linq;
using System.Reflection;
using DoomScroller.Combat;
using DoomScroller.CombatCore;
using DoomScroller.Encounters;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    // Spec 008 coverage not hit by CombatCoreAcceptanceTests / CombatCoreContractTests /
    // FightInputBuilderTests: the adapter's Revive arming and enemy/resource mapping, the
    // Revive heal floor and rounding, single-fire with several potions carried, null
    // arrays in FightInput, a JSON round-trip of an adapter-built fight, and removal of
    // manual casting (checked by reflection).
    public class CombatCoreAdapterTests
    {
        private const int Seed = 3;

        private static ItemData MakeRevivePotion(int amount)
        {
            var potion = ScriptableObject.CreateInstance<ItemData>();
            potion.category = ItemCategory.Potion;
            potion.potionEffect = PotionEffect.Revive;
            potion.potionAmount = amount;
            return potion;
        }

        private static GameSettings AutoConsumeOn()
        {
            var settings = new GameSettings();
            settings.SetAutoConsumeRevivePotions(true);
            return settings;
        }

        private static string BeatKey(CombatBeat b)
        {
            // JsonUtility turns a null string into "", so treat both as "no ability".
            var ability = string.IsNullOrEmpty(b.AbilityName) ? "-" : b.AbilityName;
            return $"{b.Actor}|{b.IsRanged}|{b.WasMiss}|{b.DisplayDamage}|{ability}|{b.IsDotTick}";
        }

        // ---- Revive arming in the adapter -------------------------------------------

        [Test]
        public void Build_ArmsTheBestRevivePotion_WhenAutoConsumeIsOn()
        {
            // Three Revives carried: only the strongest is armed (one per fight).
            var loadout = TestFixtures.MakeLoadout(MakeRevivePotion(30), MakeRevivePotion(60), MakeRevivePotion(45));

            var input = FightInputBuilder.Build(loadout, new PlayerAttributes(), AutoConsumeOn(), 50, Seed, 10, 1, 1f,
                null, true);

            Assert.IsTrue(input.reviveArmed);
            Assert.AreEqual(60, input.revivePercent);
        }

        [Test]
        public void Build_DoesNotArmRevive_WhenAutoConsumeIsOff()
        {
            var loadout = TestFixtures.MakeLoadout(MakeRevivePotion(50));

            var input = FightInputBuilder.Build(loadout, new PlayerAttributes(), new GameSettings(), 50, Seed, 10, 1,
                1f, null, true);

            Assert.IsFalse(input.reviveArmed);
            Assert.AreEqual(0, input.revivePercent);
        }

        [Test]
        public void Build_DoesNotArmRevive_WhenNoRevivePotionIsCarried()
        {
            var loadout = TestFixtures.MakeLoadout(TestFixtures.MakeWeapon(power: 5, attackSpeed: 1f));

            var input = FightInputBuilder.Build(loadout, new PlayerAttributes(), AutoConsumeOn(), 50, Seed, 10, 1, 1f,
                null, true);

            Assert.IsFalse(input.reviveArmed);
        }

        [Test]
        public void Build_NullSettings_DoesNotArmRevive_AndDoesNotThrow()
        {
            var loadout = TestFixtures.MakeLoadout(MakeRevivePotion(50));

            FightInput input = null;
            Assert.DoesNotThrow(() => input = FightInputBuilder.Build(loadout, new PlayerAttributes(), null, 50, Seed,
                10, 1, 1f, null, true));
            Assert.IsFalse(input.reviveArmed);
        }

        [Test]
        public void ForResource_NeverArmsRevive_EvenWithAutoConsumeAndAPotion()
        {
            // A resource node has no revive path at all: the adapter takes no settings.
            var loadout = TestFixtures.MakeLoadout(MakeRevivePotion(100));
            var data = ScriptableObject.CreateInstance<EncounterData>();

            var input = FightInputBuilder.ForResource(loadout, new PlayerAttributes(), data, 50, Seed);

            Assert.IsFalse(input.reviveArmed);
            Assert.IsFalse(input.enemy.canAttack);
        }

        // ---- Enemy and resource mapping -----------------------------------------------

        [Test]
        public void ForEncounter_CopiesEnemyStatsAndWeaknessesFromTheAsset()
        {
            var data = ScriptableObject.CreateInstance<EncounterData>();
            data.enemyHealth = 33;
            data.enemyAttackDamage = 9;
            data.enemyAttackSpeed = 1.3f;
            data.damageTypeModifiers = new[] { new DamageTypeModifier { type = DamageType.Slash, multiplier = 1.5f } };
            var loadout = TestFixtures.MakeLoadout();

            var input = FightInputBuilder.ForEncounter(loadout, new PlayerAttributes(), null, data, 61, 77);

            Assert.AreEqual(33, input.enemy.maxHp);
            Assert.AreEqual(9, input.enemy.attackDamage);
            Assert.AreEqual(1.3f, input.enemy.attackSpeed, 1e-6f);
            Assert.IsTrue(input.enemy.canAttack);
            Assert.AreEqual(61, input.player.startHp);
            Assert.AreEqual(77, input.seed);
            var mult = input.enemy.damageMultipliers.Single();
            Assert.AreEqual((int)DamageType.Slash, mult.damageType);
            Assert.AreEqual(1.5f, mult.multiplier, 1e-6f);
        }

        [Test]
        public void ForStep_CopiesDungeonRoomEnemyStats()
        {
            var step = new DungeonStep(depthIndex: 2, label: "Room", isRestRoom: false, isBonusTreasureRoom: false,
                isFinalTreasureRoom: false, xpReward: 0, goldReward: 0, rewardItems: null, enemyHealth: 40,
                enemyAttackDamage: 6, enemyAttackSpeed: 0.7f,
                enemyWeaknesses: new[] { new DamageTypeModifier { type = DamageType.Arcane, multiplier = 2f } },
                enemySprite: null, stepColor: default);
            var loadout = TestFixtures.MakeLoadout();

            var input = FightInputBuilder.ForStep(loadout, new PlayerAttributes(), null, step, 44, 9);

            Assert.AreEqual(40, input.enemy.maxHp);
            Assert.AreEqual(6, input.enemy.attackDamage);
            Assert.AreEqual(0.7f, input.enemy.attackSpeed, 1e-6f);
            Assert.AreEqual((int)DamageType.Arcane, input.enemy.damageMultipliers.Single().damageType);
            Assert.AreEqual(2f, input.enemy.damageMultipliers.Single().multiplier, 1e-6f);
            Assert.AreEqual(44, input.player.startHp);
        }

        [Test]
        public void ForResource_FightHasNoEnemyBeats_OnlyPlayerBeats()
        {
            var loadout = TestFixtures.MakeLoadout(TestFixtures.MakeWeapon(power: 8, attackSpeed: 2f));
            var data = ScriptableObject.CreateInstance<EncounterData>();
            var input = FightInputBuilder.ForResource(loadout, new PlayerAttributes(), data, 50, Seed);

            var sim = new CombatSimulator(input, new System.Random(Seed));
            var beats = sim.RunForDuration(5f);

            Assert.IsNotEmpty(beats);
            Assert.IsTrue(beats.All(b => b.Actor == CombatActor.Player), "a resource node produced an enemy beat");
            Assert.Greater(sim.DamageDealt, 0);
        }

        // ---- Revive heal amount, through the simulator --------------------------------

        [Test]
        public void Revive_HealsTheSamePercentOfStartHpAsTheLoadoutReports()
        {
            var potion = MakeRevivePotion(50);
            var loadout = TestFixtures.MakeLoadout(potion);
            var input = FightInputBuilder.Build(loadout, new PlayerAttributes(), AutoConsumeOn(), 40, Seed, 1000000, 100,
                1f, null, true);
            var sim = new CombatSimulator(input, new System.Random(Seed));

            var beats = sim.RunUntilDeathOrVictory();

            Assert.IsTrue(sim.ReviveFired);
            Assert.AreEqual(loadout.GetReviveHealAmount(potion, 40), sim.ReviveHp);
            Assert.AreEqual(20, sim.ReviveHp);
            Assert.AreEqual(CombatActor.Enemy, beats[sim.ReviveBeatIndex].Actor);
        }

        [Test]
        public void Revive_HealIsRoundedUp_AndNeverBelowOne()
        {
            // 10% of 33 is 3.3 -> rounds up to 4.
            var rounded = TestFixtures.MakeSim(TestFixtures.MakeLoadout(), new PlayerAttributes(), 33, 1000000, 100, 1f,
                null, new System.Random(Seed), reviveArmedPercent: 10);
            rounded.RunUntilDeathOrVictory();
            Assert.IsTrue(rounded.ReviveFired);
            Assert.AreEqual(4, rounded.ReviveHp);

            // 1% of 50 is 0.5 -> must still heal at least 1.
            var floor = TestFixtures.MakeSim(TestFixtures.MakeLoadout(), new PlayerAttributes(), 50, 1000000, 100, 1f,
                null, new System.Random(Seed), reviveArmedPercent: 1);
            floor.RunUntilDeathOrVictory();
            Assert.IsTrue(floor.ReviveFired);
            Assert.AreEqual(1, floor.ReviveHp);
        }

        [Test]
        public void Revive_FiresAtMostOnce_EvenWithThreePotionsCarried()
        {
            var loadout = TestFixtures.MakeLoadout(MakeRevivePotion(100), MakeRevivePotion(100), MakeRevivePotion(100));
            var input = FightInputBuilder.Build(loadout, new PlayerAttributes(), AutoConsumeOn(), 20, Seed, 1000000, 100,
                1f, null, true);

            var result = CombatSimulator.Resolve(input);

            Assert.IsTrue(result.reviveFired);
            Assert.AreEqual(FightOutcome.Loss, result.outcome, "a second death after the one Revive must end the fight");
            Assert.AreEqual(0, result.finalPlayerHp);
            Assert.GreaterOrEqual(result.reviveBeatIndex, 0);
        }

        [Test]
        public void Revive_NotNeeded_DoesNotFire()
        {
            // Player wins outright: the armed Revive stays unused.
            var loadout = TestFixtures.MakeLoadout(TestFixtures.MakeWeapon(power: 30, attackSpeed: 2f));
            var input = FightInputBuilder.Build(loadout, new PlayerAttributes(), AutoConsumeOn(), 200, Seed, 5, 1, 0.5f,
                null, true);
            input.revivePercent = 50;

            var result = CombatSimulator.Resolve(input);

            Assert.AreEqual(FightOutcome.Win, result.outcome);
            Assert.IsFalse(result.reviveFired);
            Assert.AreEqual(-1, result.reviveBeatIndex);
        }

        // ---- Input edge cases and serialization --------------------------------------

        [Test]
        public void Resolve_NullArraysInInput_DoNotThrow()
        {
            // JsonUtility hands back null for a "null" array, so the sim must cope with it.
            var input = new FightInput
            {
                seed = Seed,
                player = new PlayerSideInput { startHp = 30, attributes = null, weapons = null, abilities = null, passives = null },
                enemy = new EnemySideInput { maxHp = 20, attackDamage = 4, attackSpeed = 1f, damageMultipliers = null }
            };

            Assert.DoesNotThrow(() => CombatSimulator.Resolve(input));
        }

        [Test]
        public void AdapterBuiltFight_SurvivesJsonRoundTripWithIdenticalResult()
        {
            var ability = new WeaponAbilityData
            {
                abilityName = "Rend", powerBonus = 2, cooldownSeconds = 2.5f,
                onCastEffects = new[]
                {
                    new StatusEffectData { effectType = StatusEffectType.DamageOverTime, magnitude = 3f, durationSeconds = 4f, tickIntervalSeconds = 1f }
                }
            };
            var weapon = TestFixtures.MakeWeapon(power: 10, attackSpeed: 1.1f, damageType: DamageType.Blunt,
                abilities: new[] { ability });
            var loadout = TestFixtures.MakeLoadout(weapon);
            var input = FightInputBuilder.Build(loadout, new PlayerAttributes(), AutoConsumeOn(), 55, 2024, 70, 9, 0.9f,
                new[] { new DamageTypeModifier { type = DamageType.Blunt, multiplier = 1.25f } }, true);

            var restored = JsonUtility.FromJson<FightInput>(JsonUtility.ToJson(input));

            Assert.AreEqual(JsonUtility.ToJson(input), JsonUtility.ToJson(restored));
            var original = CombatSimulator.Resolve(input);
            var replay = CombatSimulator.Resolve(restored);
            Assert.AreEqual(original.outcome, replay.outcome);
            Assert.AreEqual(original.finalPlayerHp, replay.finalPlayerHp);
            Assert.AreEqual(original.finalEnemyHp, replay.finalEnemyHp);
            CollectionAssert.AreEqual(original.beats.Select(BeatKey).ToList(), replay.beats.Select(BeatKey).ToList());
        }

        [Test]
        public void FightResult_JsonRoundTrip_KeepsBeatsAndRevive()
        {
            var input = new FightInput
            {
                seed = Seed,
                reviveArmed = true,
                revivePercent = 40,
                player = new PlayerSideInput { startHp = 5 },
                enemy = new EnemySideInput { maxHp = 500, attackDamage = 50, attackSpeed = 1f }
            };
            var result = CombatSimulator.Resolve(input);
            Assert.IsTrue(result.reviveFired);

            var restored = JsonUtility.FromJson<FightResult>(JsonUtility.ToJson(result));

            Assert.AreEqual(result.outcome, restored.outcome);
            Assert.AreEqual(result.reviveFired, restored.reviveFired);
            Assert.AreEqual(result.reviveBeatIndex, restored.reviveBeatIndex);
            Assert.AreEqual(result.finalPlayerHp, restored.finalPlayerHp);
            Assert.AreEqual(result.finalEnemyHp, restored.finalEnemyHp);
            CollectionAssert.AreEqual(result.beats.Select(BeatKey).ToList(), restored.beats.Select(BeatKey).ToList());
        }

        [Test]
        public void AdapterBuiltEncounter_SameSeed_GivesIdenticalResult()
        {
            var data = ScriptableObject.CreateInstance<EncounterData>();
            data.enemyHealth = 60;
            data.enemyAttackDamage = 8;
            data.enemyAttackSpeed = 1f;
            var loadout = TestFixtures.MakeLoadout(TestFixtures.MakeWeapon(power: 7, attackSpeed: 1.4f));

            foreach (var seed in new[] { 1, 42, 9001 })
            {
                var a = CombatSimulator.Resolve(FightInputBuilder.ForEncounter(loadout, new PlayerAttributes(), null, data, 50, seed));
                var b = CombatSimulator.Resolve(FightInputBuilder.ForEncounter(loadout, new PlayerAttributes(), null, data, 50, seed));
                Assert.AreEqual(JsonUtility.ToJson(a), JsonUtility.ToJson(b), "seed " + seed);
            }
        }

        // ---- Removal of manual casting (criterion 5) ----------------------------------

        [Test]
        public void ManualCasting_IsGoneFromSimulatorSettingsSaveAndViews()
        {
            var types = new[] { typeof(CombatSimulator), typeof(CombatPlaybackView), typeof(GameSettings), typeof(SaveData), typeof(ProfileView) };
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

            foreach (var type in types)
            {
                var leftovers = type.GetMembers(all).Where(m => m.Name.IndexOf("manual", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(m => m.Name).ToArray();
                Assert.IsEmpty(leftovers, type.Name + " still has manual-casting members: " + string.Join(", ", leftovers));
            }
        }

        [Test]
        public void SaveData_RoundTripsAutoConsumeAndDropsManualField()
        {
            var save = new SaveData();
            save.autoConsumeRevivePotions = true;

            var restored = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));

            Assert.IsTrue(restored.autoConsumeRevivePotions);
            StringAssert.DoesNotContain("manualAbilityCasting", JsonUtility.ToJson(save));
        }
    }
}
