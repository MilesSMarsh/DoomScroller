using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DoomScroller.Combat;
using DoomScroller.CombatCore;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    // Spec 008 contract checks not covered by CombatCoreAcceptanceTests:
    //  - the same seeded fight replays identically (device badge/replay depends on it);
    //  - FightInput/FightResult JSON round-trips with equal values (016 sends them over the wire).
    // The CombatCore types don't exist yet, so the last two look them up by reflection and
    // stay red until the types land.
    public class CombatCoreContractTests
    {
        private const string CoreNamespace = "DoomScroller.CombatCore";

        private static Type FindCoreType(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(CoreNamespace + "." + name, throwOnError: false))
                .FirstOrDefault(t => t != null);
        }

        private static string BeatKey(CombatBeat b)
        {
            return $"{b.Actor}|{b.IsRanged}|{b.WasMiss}|{b.DisplayDamage}|{b.AbilityName ?? "-"}|{b.IsDotTick}";
        }

        private static List<string> RunFight(int seed)
        {
            var loadout = TestFixtures.MakeLoadout(
                TestFixtures.MakeWeapon(power: 9, attackSpeed: 1.2f, slot: EquipmentSlotType.RightHand));
            var sim = TestFixtures.MakeSim(loadout, new PlayerAttributes(), playerStartHp: 60,
                enemyMaxHealth: 75, enemyAttackDamage: 7, enemyAttackSpeed: 0.9f, enemyWeaknesses: null,
                rng: new System.Random(seed));
            var beats = sim.RunUntilDeathOrVictory();
            var keys = beats.Select(BeatKey).ToList();
            keys.Add($"END|{sim.IsPlayerDead}|{sim.PlayerHp}|{sim.EnemyHp}");
            return keys;
        }

        [Test]
        public void SameSeed_ReplaysIdenticalBeatsAndOutcome()
        {
            foreach (var seed in new[] { 1, 7, 42, 1234, 99991 })
            {
                var first = RunFight(seed);
                var second = RunFight(seed);
                CollectionAssert.AreEqual(first, second, "seed " + seed + " diverged between runs");
            }
        }

        [Test]
        public void DifferentSeeds_CanProduceDifferentFights()
        {
            // Guards against the seed being ignored: across several seeds at least one
            // fight must differ from seed 1, otherwise the RNG isn't wired in.
            var baseline = RunFight(1);
            var anyDifferent = Enumerable.Range(2, 20).Any(s => !RunFight(s).SequenceEqual(baseline));
            Assert.IsTrue(anyDifferent, "every seed produced the same fight; the seed is not used");
        }

        [TestCase("FightInput")]
        [TestCase("FightResult")]
        public void CombatCore_JsonRoundTripPreservesValues(string typeName)
        {
            var type = FindCoreType(typeName);
            Assert.IsNotNull(type, CoreNamespace + "." + typeName + " does not exist yet");

            var original = Activator.CreateInstance(type);
            var json = JsonUtility.ToJson(original);
            var restored = JsonUtility.FromJson(json, type);
            Assert.IsNotNull(restored);
            Assert.AreEqual(json, JsonUtility.ToJson(restored), typeName + " changed across a JSON round-trip");
        }

        [Test]
        public void CombatCore_FightResolverIsDeterministicForSameInput()
        {
            var inputType = FindCoreType("FightInput");
            var resultType = FindCoreType("FightResult");
            Assert.IsNotNull(inputType, "FightInput does not exist yet");
            Assert.IsNotNull(resultType, "FightResult does not exist yet");

            // Find the public static entry point: (FightInput) -> FightResult.
            var entry = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t => t.Namespace == CoreNamespace)
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .FirstOrDefault(m => m.ReturnType == resultType && m.GetParameters().Length == 1 &&
                                     m.GetParameters()[0].ParameterType == inputType);
            Assert.IsNotNull(entry, "no public static resolver (FightInput) -> FightResult in " + CoreNamespace);

            // Populate the input's numeric fields with small positive values so both sides
            // can act; reference fields (arrays, optional Revive) stay null/default.
            var input = Activator.CreateInstance(inputType);
            foreach (var f in inputType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (f.FieldType == typeof(int)) f.SetValue(input, 20);
                else if (f.FieldType == typeof(float)) f.SetValue(input, 1f);
            }
            var seedField = inputType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(f => f.Name.IndexOf("seed", StringComparison.OrdinalIgnoreCase) >= 0 && f.FieldType == typeof(int));
            Assert.IsNotNull(seedField, "FightInput has no int seed field");
            seedField.SetValue(input, 4242);

            var a = entry.Invoke(null, new[] { input });
            var b = entry.Invoke(null, new[] { input });
            Assert.AreEqual(JsonUtility.ToJson(a), JsonUtility.ToJson(b),
                "same FightInput + seed produced different FightResults");
        }
    }
}
