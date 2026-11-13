using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.CombatCore.Tests
{
    // Tests CombatCore on its own: this assembly references nothing but CombatCore, so a
    // fight is resolved from a hand-built FightInput with no game types involved.
    public class CombatCoreTests
    {
        private static FightInput MakeInput(int seed = 1234, bool revive = false)
        {
            return new FightInput
            {
                seed = seed,
                reviveArmed = revive,
                revivePercent = revive ? 50 : 0,
                player = new PlayerSideInput
                {
                    startHp = 60,
                    defense = 2,
                    attributes = new[] { 5, 5, 5, 5 },
                    weapons = new[]
                    {
                        new WeaponInput
                        {
                            power = 9, attackSpeed = 1.2f, damageType = 2,
                            scaling = new[] { new ScalingInput { attribute = AttributeIndex.Constitution, coefficient = 0.5f } }
                        }
                    },
                    abilities = new[]
                    {
                        new AbilityInput
                        {
                            name = "Rend", weaponIndex = 0, powerBonus = 4, cooldownSeconds = 3f,
                            onCastEffects = new[]
                            {
                                new EffectInput { effectType = StatusEffectType.DamageOverTime, magnitude = 2f, durationSeconds = 3f, tickIntervalSeconds = 1f }
                            }
                        }
                    },
                    passives = new[] { new PassiveInput { effectType = StatusEffectType.EmpowerDamage, magnitude = 0.1f } }
                },
                enemy = new EnemySideInput
                {
                    maxHp = 90, attackDamage = 14, attackSpeed = 0.9f,
                    damageMultipliers = new[] { new DamageMultiplierInput { damageType = 2, multiplier = 1.5f } }
                }
            };
        }

        private static string Key(FightResult r)
        {
            return r.outcome + "|" + r.reviveFired + "|" + r.reviveBeatIndex + "|" + r.finalPlayerHp + "|" + r.finalEnemyHp + "|" +
                string.Join(";", r.beats.Select(b => $"{b.Actor},{b.IsRanged},{b.WasMiss},{b.DisplayDamage},{b.AbilityName},{b.IsDotTick}"));
        }

        [Test]
        public void SameInputAndSeed_GivesIdenticalResultTwice()
        {
            foreach (var seed in new[] { 1, 7, 42, 99991 })
            {
                var a = CombatSimulator.Resolve(MakeInput(seed));
                var b = CombatSimulator.Resolve(MakeInput(seed));
                Assert.IsNotEmpty(a.beats);
                Assert.AreEqual(Key(a), Key(b), "seed " + seed);
            }
        }

        [Test]
        public void DifferentSeeds_CanGiveDifferentFights()
        {
            var baseline = Key(CombatSimulator.Resolve(MakeInput(1)));
            Assert.IsTrue(Enumerable.Range(2, 20).Any(s => Key(CombatSimulator.Resolve(MakeInput(s))) != baseline));
        }

        [Test]
        public void FightInput_RoundTripsThroughJsonUtility()
        {
            var input = MakeInput(77, revive: true);
            var restored = JsonUtility.FromJson<FightInput>(JsonUtility.ToJson(input));

            Assert.AreEqual(JsonUtility.ToJson(input), JsonUtility.ToJson(restored));
            Assert.AreEqual(77, restored.seed);
            Assert.IsTrue(restored.reviveArmed);
            Assert.AreEqual(50, restored.revivePercent);
            Assert.AreEqual("Rend", restored.player.abilities[0].name);
            Assert.AreEqual(StatusEffectType.DamageOverTime, restored.player.abilities[0].onCastEffects[0].effectType);
            Assert.AreEqual(1.5f, restored.enemy.damageMultipliers[0].multiplier);
            // The restored input resolves to the same fight as the original.
            Assert.AreEqual(Key(CombatSimulator.Resolve(input)), Key(CombatSimulator.Resolve(restored)));
        }

        [Test]
        public void FightResult_RoundTripsThroughJsonUtility()
        {
            var result = CombatSimulator.Resolve(MakeInput(5, revive: true));
            var restored = JsonUtility.FromJson<FightResult>(JsonUtility.ToJson(result));

            Assert.AreEqual(Key(result), Key(restored));
            Assert.AreEqual(result.beats.Length, restored.beats.Length);
        }

        [Test]
        public void ArmedRevive_FiresInsideTheSim_AtMostOnce()
        {
            // A fight the player can't survive: 5 HP against heavy hits and a huge enemy.
            var input = MakeInput(3, revive: true);
            input.player.startHp = 5;
            input.enemy.maxHp = 1000000;
            input.enemy.attackDamage = 100;
            var result = CombatSimulator.Resolve(input);

            Assert.IsTrue(result.reviveFired);
            Assert.AreEqual(CombatActor.Enemy, result.beats[result.reviveBeatIndex].Actor);
            Assert.AreEqual(FightOutcome.Loss, result.outcome);
            Assert.AreEqual(0, result.finalPlayerHp);

            input.reviveArmed = false;
            var unarmed = CombatSimulator.Resolve(input);
            Assert.IsFalse(unarmed.reviveFired);
            Assert.AreEqual(-1, unarmed.reviveBeatIndex);
            Assert.Less(unarmed.beats.Length, result.beats.Length);
        }

        [Test]
        public void DefaultInput_ResolvesWithoutThrowing()
        {
            Assert.DoesNotThrow(() => CombatSimulator.Resolve(new FightInput()));
        }
    }
}
